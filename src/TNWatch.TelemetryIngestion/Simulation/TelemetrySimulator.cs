using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Telemetry;

namespace TNWatch.TelemetryIngestion.Simulation;

/// <summary>
/// In-process TrafficNode simulator (dashboard toggle "Simulation | Aus").
/// Produces payloads in the real <c>sample_msg.json</c> format and writes them into the ingestion channel,
/// so parser, validation, persistence and status evaluation run exactly as for MQTT data.
/// It never publishes to the shared course broker.
/// </summary>
public sealed class TelemetrySimulator : BackgroundService, ISimulatorControl
{
    private readonly SimulatorOptions _options;
    private readonly IngestionChannel _channel;
    private readonly IFleetNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<TelemetrySimulator> _logger;
    private readonly IReadOnlyList<SimulatedNode> _nodes;
    private readonly Random _random;
    private readonly string _topicTemplate;
    private readonly TimeZoneInfo _eventTimeZone;
    private volatile bool _running;

    public TelemetrySimulator(
        IOptions<SimulatorOptions> options,
        IOptions<IngestionOptions> ingestionOptions,
        IngestionChannel channel,
        IFleetNotifier notifier,
        TimeProvider time,
        ILogger<TelemetrySimulator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ingestionOptions);
        _options = options.Value;
        _channel = channel;
        _notifier = notifier;
        _time = time;
        _logger = logger;
        _running = _options.StartEnabled;
#pragma warning disable CA5394 // Deterministic pseudo-random demo data.
        _random = new Random(_options.Seed);
#pragma warning restore CA5394
        _topicTemplate = ingestionOptions.Value.TelemetryTopicPatterns.FirstOrDefault() ?? "simulation/{nodeId}";
        _eventTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ingestionOptions.Value.EventTimeZone);

        var demo = SimulatedNode.DemoFleet();
        var nodes = demo.Take(_options.NodeCount).ToList();
        for (var i = nodes.Count + 1; i <= _options.NodeCount; i++)
        {
            nodes.Add(SimulatedNode.Random(i, _random));
        }

        _nodes = nodes;
    }

    public event Func<bool, Task>? RunningChanged;

    public bool IsRunning => _running;

    public IReadOnlyList<SimulatedNode> Nodes => _nodes;

    public async Task SetRunningAsync(bool running)
    {
        if (_running == running)
        {
            return;
        }

        _running = running;
        _logger.LogInformation("Simulator {State}", running ? "started" : "stopped");
        if (running)
        {
            await TickAsync(CancellationToken.None);
        }

        if (RunningChanged is { } handler)
        {
            await handler(running);
        }

        // Simulated nodes are shown only while the simulator runs → UI must reload the fleet.
        await _notifier.PublishFleetResetAsync();
    }

    /// <summary>
    /// Emits one measurement per node, plus battery events (F10): the initial installation after a node's
    /// first measurement, and an exchange when a discharging battery is empty. Public for tests.
    /// </summary>
    public async Task TickAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        foreach (var node in _nodes)
        {
            if (node.Profile == SimulationProfile.Silent)
            {
                if (!node.HasSentInitial)
                {
                    // One message from 40 minutes ago → the node is immediately "Offline".
                    node.HasSentInitial = true;
                    await WriteAsync(node, now.AddMinutes(-40), node.NextSample(_random), now, ct);
                    await AnnounceBatteryAsync(node, now, ct);
                }

                continue;
            }

            if (node.ExchangeBatteryIfEmpty() is { } oldSerial)
            {
                _logger.LogInformation("Simulated battery exchange at {NodeId}: {Old} → {New}", node.Id, oldSerial, node.BatterySerial);
                await WriteBatteryEventAsync(node, Guid.NewGuid(), oldSerial, now, ct);
            }

            await WriteAsync(node, now, node.NextSample(_random), now, ct);
            await AnnounceBatteryAsync(node, now, ct);
        }
    }

    /// <summary>
    /// Builds a battery exchange event in the format of data/exchange.example.json
    /// (local date/time without offset, interpreted in <c>Ingestion:EventTimeZone</c>).
    /// </summary>
    public static string BuildExchangePayload(SimulatedNode node, Guid eventId, string? oldSerial, DateTimeOffset occurredAt, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(node);
        var local = TimeZoneInfo.ConvertTime(occurredAt, timeZone);
        return JsonSerializer.Serialize(new
        {
            eventId,
            date = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            time = local.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            newBat = node.BatterySerial,
            oldBat = oldSerial,
            trafficNode = node.Id,
            position = new { latitude = node.Latitude, longitude = node.Longitude, accuracy = 5 },
        });
    }

    /// <summary>Stable event ID for the initial installation, so restarting the simulator creates no duplicates.</summary>
    public static Guid InitialInstallationEventId(string nodeId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"tnwatch-simulator-battery-{nodeId}")).AsSpan(0, 16));

    private async Task AnnounceBatteryAsync(SimulatedNode node, DateTimeOffset now, CancellationToken ct)
    {
        // Sent after the node's first measurement, so the node exists when the event is processed (single consumer, FIFO).
        if (node.HasAnnouncedBattery)
        {
            return;
        }

        node.HasAnnouncedBattery = true;
        await WriteBatteryEventAsync(node, InitialInstallationEventId(node.Id), null, now, ct);
    }

    private ValueTask WriteBatteryEventAsync(SimulatedNode node, Guid eventId, string? oldSerial, DateTimeOffset now, CancellationToken ct)
    {
        var topic = $"simulation/{_options.Tenant}/battery/{node.Id}";
        var payload = Encoding.UTF8.GetBytes(BuildExchangePayload(node, eventId, oldSerial, now, _eventTimeZone));
        return _channel.Writer.WriteAsync(new RawMessage(topic, payload, now, TelemetrySource.Simulator), ct);
    }

    /// <summary>Builds a payload in the format of data/sample_msg.json (mV, mA).</summary>
    public static string BuildPayload(SimulatedNode node, DateTimeOffset measuredAt, double voltage, double current)
    {
        ArgumentNullException.ThrowIfNull(node);
        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"current_mA":{{current * 1000:F1}},"voltage_mV":{{voltage * 1000:F1}},"lat":{{node.Latitude}},"lon":{{node.Longitude}},"altitude_m":280.0,"satellites":9,"time":"{{measuredAt.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}}","temperature_C":12.5}""");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval, _time);
        do
        {
            if (!_running)
            {
                continue;
            }

            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private ValueTask WriteAsync(SimulatedNode node, DateTimeOffset measuredAt, (double Voltage, double Current) sample, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var topic = _topicTemplate
            .Replace("{tenant}", _options.Tenant, StringComparison.Ordinal)
            .Replace("{nodeId}", node.Id, StringComparison.Ordinal)
            .Replace("{*}", "sim", StringComparison.Ordinal);
        var payload = Encoding.UTF8.GetBytes(BuildPayload(node, measuredAt, sample.Voltage, sample.Current));
        return _channel.Writer.WriteAsync(new RawMessage(topic, payload, receivedAt, TelemetrySource.Simulator), ct);
    }
}
