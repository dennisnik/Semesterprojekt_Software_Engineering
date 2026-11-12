using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Telemetry;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion;

/// <summary>
/// Single consumer of the ingestion channel: classify → parse/validate (F01) → dispatch to handlers.
/// Each message runs in its own DI scope (own DbContext) and failures never stop the loop.
/// </summary>
public sealed class IngestionPipelineService(
    IngestionChannel channel,
    TelemetryParser telemetryParser,
    BatteryEventParser batteryParser,
    IServiceScopeFactory scopes,
    IngestionStats stats,
    ILogger<IngestionPipelineService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // One bad message (or a transient DB error) must not stop ingestion.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Processing failed for message on {Topic}", message.Topic);
            }
        }
    }

    /// <summary>Processes one message. Public for tests.</summary>
    public async Task ProcessAsync(RawMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        switch (Classify(message))
        {
            case MessageKind.Telemetry:
                var telemetry = telemetryParser.Parse(message);
                if (!telemetry.IsSuccess)
                {
                    Reject(message, telemetry.Error);
                    return;
                }

                await using (var scope = scopes.CreateAsyncScope())
                {
                    await scope.ServiceProvider.GetRequiredService<ITelemetryHandler>().HandleAsync(telemetry.Value!, ct);
                }

                break;

            case MessageKind.BatteryEvent:
                var battery = batteryParser.Parse(message);
                if (!battery.IsSuccess)
                {
                    Reject(message, battery.Error);
                    return;
                }

                await using (var scope = scopes.CreateAsyncScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IBatteryLifecycleHandler>().HandleAsync(battery.Value!, ct);
                }

                break;

            default:
                logger.LogDebug("Ignoring message on unhandled topic {Topic}", message.Topic);
                break;
        }
    }

    internal MessageKind Classify(RawMessage message)
    {
        if (batteryParser.Matches(message.Topic) || BatteryEventParser.LooksLikeBatteryEvent(message.Payload.Span))
        {
            return MessageKind.BatteryEvent;
        }

        if (telemetryParser.Matches(message.Topic) || message.Source == TelemetrySource.Simulator)
        {
            return MessageKind.Telemetry;
        }

        return MessageKind.Unknown;
    }

    private void Reject(RawMessage message, string? reason)
    {
        stats.RecordRejected();
        logger.LogWarning("Rejected message on {Topic}: {Reason}", message.Topic, reason);
    }

    internal enum MessageKind
    {
        Unknown,
        Telemetry,
        BatteryEvent,
    }
}
