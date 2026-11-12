using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;

namespace TNWatch.WebUI.State;

/// <summary>
/// Per-circuit view state of the fleet (scoped). Loads the fleet once, then applies live updates from the
/// in-process notifier. UI change notifications are coalesced (max. ~3 renders/s) so bursts of telemetry
/// (NFA01 load) do not flood the browser.
/// </summary>
public sealed class FleetViewModel : IAsyncDisposable
{
    private static readonly TimeSpan RenderThrottle = TimeSpan.FromMilliseconds(350);

    private readonly IFleetQueries _queries;
    private readonly IFleetNotifier _notifier;
    private readonly ISimulatorControl _simulator;
    private readonly ILogger<FleetViewModel> _logger;
    private readonly Dictionary<string, NodeSnapshot> _nodes = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private PeriodicTimer? _timer;
    private Task? _loop;
    private bool _dirty;
    private bool _initialized;
    private IReadOnlyList<NodeSnapshot> _sorted = [];

    public FleetViewModel(IFleetQueries queries, IFleetNotifier notifier, ISimulatorControl simulator, ILogger<FleetViewModel> logger)
    {
        _queries = queries;
        _notifier = notifier;
        _simulator = simulator;
        _logger = logger;
    }

    /// <summary>Raised (throttled) after the fleet changed. Handlers must marshal to the renderer (InvokeAsync).</summary>
    public event Func<Task>? Changed;

    /// <summary>All visible nodes, most urgent first (Kritisch, Warnung, Offline, Unbekannt, Normal), then by ID.</summary>
    public IReadOnlyList<NodeSnapshot> Nodes => _sorted;

    public FleetSummary Summary => _sorted.Summarize();

    public bool SimulatorRunning => _simulator.IsRunning;

    /// <summary>Monotonic version, incremented with every applied change (lets components detect changes cheaply).</summary>
    public int Version { get; private set; }

    public NodeSnapshot? Find(string? id)
    {
        if (id is null)
        {
            return null;
        }

        lock (_sync)
        {
            return _nodes.GetValueOrDefault(id);
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ReloadAsync();
        _notifier.NodeUpdated += OnNodeUpdated;
        _notifier.FleetReset += OnFleetReset;
        _timer = new PeriodicTimer(RenderThrottle);
        _loop = RunAsync(_timer);
    }

    public Task SetSimulatorAsync(bool running) => _simulator.SetRunningAsync(running);

    public static int Severity(NodeStatus status) => status switch
    {
        NodeStatus.Critical => 0,
        NodeStatus.Warning => 1,
        NodeStatus.Offline => 2,
        NodeStatus.Unknown => 3,
        _ => 4,
    };

    public async ValueTask DisposeAsync()
    {
        _notifier.NodeUpdated -= OnNodeUpdated;
        _notifier.FleetReset -= OnFleetReset;
        _timer?.Dispose();
        if (_loop is not null)
        {
            await _loop;
        }
    }

    private async Task ReloadAsync()
    {
        var nodes = await _queries.GetNodesAsync(_simulator.IsRunning, CancellationToken.None);
        lock (_sync)
        {
            _nodes.Clear();
            foreach (var n in nodes)
            {
                _nodes[n.Id] = n;
            }

            Rebuild();
        }
    }

    private Task OnNodeUpdated(NodeSnapshot snapshot)
    {
        if (snapshot.IsSimulated && !_simulator.IsRunning)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            _nodes[snapshot.Id] = snapshot;
            _dirty = true;
        }

        return Task.CompletedTask;
    }

    private async Task OnFleetReset()
    {
        await ReloadAsync();
        lock (_sync)
        {
            _dirty = true;
        }
    }

    private async Task RunAsync(PeriodicTimer timer)
    {
        try
        {
            while (await timer.WaitForNextTickAsync())
            {
                bool notify;
                lock (_sync)
                {
                    notify = _dirty;
                    if (_dirty)
                    {
                        Rebuild();
                        _dirty = false;
                    }
                }

                if (notify && Changed is { } handler)
                {
                    await handler();
                }
            }
        }
#pragma warning disable CA1031 // A disposed circuit must not crash the server.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogDebug(ex, "Fleet view loop ended");
        }
    }

    private void Rebuild()
    {
        _sorted = _nodes.Values
            .OrderBy(n => Severity(n.Status))
            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Version++;
    }
}
