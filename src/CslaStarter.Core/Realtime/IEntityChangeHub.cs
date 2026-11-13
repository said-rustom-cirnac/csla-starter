namespace CslaStarter.Core.Realtime;

/// <summary>
/// This instance's subscriptions. Dispatch reaches only the subscribers of a change's record topic and type topic.
/// Handlers run concurrently and must return quickly (schedule work, don't do it inline); one failing handler never
/// stops the others. Delivery is at-most-once: after a backplane outage the hub raises Resync instead.
/// </summary>
public interface IEntityChangeHub
{
    IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler);

    /// <summary>Called after notices may have been missed: lists refresh, editors check their version.</summary>
    IDisposable OnResync(Func<Task> handler);

    Task DispatchAsync(EntityChange change);

    Task RaiseResyncAsync();
}
