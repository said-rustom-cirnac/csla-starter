using CslaStarter.Core.Realtime;

namespace CslaStarter.Infrastructure.Realtime;

public enum PresenceAction
{
    Join,
    Leave,
    Heartbeat,
}

/// <summary>One presence change as instances exchange it. The topic travels as its type and key.</summary>
public sealed record PresenceMessage(PresenceAction Action, string EntityType, string? EntityKey, PresenceEntry Entry);

/// <summary>Carries presence between instances. Messages received are other instances' only, with the sender's id.</summary>
public interface IPresenceTransport
{
    Task SendAsync(IReadOnlyList<PresenceMessage> messages, CancellationToken cancellationToken);

    IDisposable Subscribe(Func<string, PresenceMessage, Task> received);
}

/// <summary>One instance: nobody else to tell.</summary>
public sealed class InMemoryPresenceTransport : IPresenceTransport
{
    public Task SendAsync(IReadOnlyList<PresenceMessage> messages, CancellationToken cancellationToken) => Task.CompletedTask;

    public IDisposable Subscribe(Func<string, PresenceMessage, Task> received) => NoSubscription.Instance;

    private sealed class NoSubscription : IDisposable
    {
        public static readonly NoSubscription Instance = new();

        public void Dispose()
        {
        }
    }
}
