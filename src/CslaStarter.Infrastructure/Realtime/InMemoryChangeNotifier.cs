using CslaStarter.Core.Realtime;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>The single-instance notifier: committed changes go straight to this process's hub.</summary>
public sealed class InMemoryChangeNotifier(IEntityChangeHub hub) : IEntityChangeNotifier
{
    public async Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            await hub.DispatchAsync(change).ConfigureAwait(false);
        }
    }
}
