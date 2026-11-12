namespace TNWatch.Domain.Fleet;

/// <summary>Thread-safe default implementation of <see cref="IFleetNotifier"/>. Subscriber failures are isolated.</summary>
public sealed class FleetNotifier : IFleetNotifier
{
    public event Func<NodeSnapshot, Task>? NodeUpdated;

    public event Func<StatusChange, Task>? StatusChanged;

    public event Func<Task>? FleetReset;

    public Task PublishNodeUpdatedAsync(NodeSnapshot snapshot) => InvokeAll(NodeUpdated, h => h(snapshot));

    public Task PublishStatusChangedAsync(StatusChange change) => InvokeAll(StatusChanged, h => h(change));

    public Task PublishFleetResetAsync() => InvokeAll(FleetReset, h => h());

    private static async Task InvokeAll<THandler>(THandler? multicast, Func<THandler, Task> invoke)
        where THandler : Delegate
    {
        if (multicast is null)
        {
            return;
        }

        foreach (var handler in multicast.GetInvocationList().Cast<THandler>())
        {
            try
            {
                await invoke(handler);
            }
#pragma warning disable CA1031 // A broken UI circuit must never stop ingestion.
            catch (Exception)
#pragma warning restore CA1031
            {
                // Intentionally swallowed: subscribers are UI circuits that may already be disposed.
            }
        }
    }
}
