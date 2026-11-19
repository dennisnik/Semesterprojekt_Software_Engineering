using Microsoft.AspNetCore.SignalR;
using TNWatch.Domain.Assignments;
using TNWatch.Domain.Fleet;

namespace TNWatch.WebUI.Realtime;

/// <summary>SignalR hub for external real-time clients (ADR-02). Requires authentication (fallback policy).</summary>
public sealed class TelemetryHub : Hub
{
    public const string Path = "/hubs/telemetry";
}

/// <summary>Bridges in-process fleet events to all hub clients ("nodeUpdated", "statusChanged", "assignmentChanged").</summary>
public sealed class HubBroadcaster(IFleetNotifier notifier, IHubContext<TelemetryHub> hub) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        notifier.NodeUpdated += OnNodeUpdated;
        notifier.StatusChanged += OnStatusChanged;
        notifier.AssignmentChanged += OnAssignmentChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        notifier.NodeUpdated -= OnNodeUpdated;
        notifier.StatusChanged -= OnStatusChanged;
        notifier.AssignmentChanged -= OnAssignmentChanged;
        return Task.CompletedTask;
    }

    private Task OnNodeUpdated(NodeSnapshot snapshot) => hub.Clients.All.SendAsync("nodeUpdated", snapshot);

    private Task OnStatusChanged(StatusChange change) => hub.Clients.All.SendAsync("statusChanged", change);

    private Task OnAssignmentChanged(AssignmentChange change) => hub.Clients.All.SendAsync("assignmentChanged", change);
}
