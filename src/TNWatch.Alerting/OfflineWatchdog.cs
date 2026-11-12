using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;

namespace TNWatch.Alerting;

public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

    /// <summary>How often the watchdog checks for nodes without contact.</summary>
    public TimeSpan OfflineCheckInterval { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// A node that stops sending never triggers a message, so this background job periodically switches
/// stale nodes to Offline (README §7: last contact ≥ 30 min).
/// </summary>
public sealed class OfflineWatchdog(
    IServiceScopeFactory scopes,
    StatusEvaluator evaluator,
    IFleetNotifier notifier,
    TimeProvider time,
    IOptions<AlertingOptions> options,
    ILogger<OfflineWatchdog> logger) : BackgroundService
{
    /// <summary>Runs one check. Public for tests.</summary>
    public async Task<int> CheckOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();

        var stale = await repository.GetStaleNodesAsync(now - evaluator.Rules.OfflineAfter, ct);
        var changes = new List<(TrafficNode Node, StatusChange Change)>();
        foreach (var node in stale)
        {
            if (node.MarkOfflineIfStale(evaluator, now) is { } change)
            {
                repository.AddStatusChange(change);
                changes.Add((node, change));
            }
        }

        if (changes.Count == 0)
        {
            return 0;
        }

        await repository.SaveChangesAsync(ct);
        foreach (var (node, change) in changes)
        {
            logger.LogInformation("Node {NodeId} is offline (last contact {LastSeen:O})", node.Id, node.LastSeenAt);
            await notifier.PublishNodeUpdatedAsync(node.ToSnapshot());
            await notifier.PublishStatusChangedAsync(change);
        }

        return changes.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.OfflineCheckInterval, time);
        do
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The watchdog must survive transient DB errors.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Offline check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
