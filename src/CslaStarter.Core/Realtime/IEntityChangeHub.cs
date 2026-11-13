namespace CslaStarter.Core.Realtime;

/// <summary>
/// This instance's subscriptions. Dispatch reaches only the subscribers of a change's record topic and type topic. It
/// starts every handler and returns without waiting for them: each runs inline until its first await, so keep that part
/// short and never block. One failing handler never stops the others. Handlers run on whatever thread the notice
/// arrived on, not a circuit's renderer (components hop with InvokeAsync). Delivery is at-most-once: after a
/// backplane outage the hub raises Resync instead.
/// </summary>
public interface IEntityChangeHub
{
    IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler);

    /// <summary>Called after notices may have been missed: lists refresh, editors check their version.</summary>
    IDisposable OnResync(Func<Task> handler);

    Task DispatchAsync(EntityChange change);

    Task RaiseResyncAsync();
}
