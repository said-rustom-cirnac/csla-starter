using System.Collections.Concurrent;
using System.Collections.Immutable;
using CslaStarter.Core.Realtime;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// Copy-on-write subscription lists per topic, so dispatch reads one array without locking and touches only the
/// subscribers of the change's two topics. Dispatch starts every handler and returns without waiting for them, so a
/// slow screen never holds up a save or the backplane. One instance per process (singleton).
/// </summary>
public sealed partial class EntityChangeHub(ILogger<EntityChangeHub> logger) : IEntityChangeHub
{
    private readonly ConcurrentDictionary<ChangeTopic, ImmutableArray<Subscription<EntityChange>>> _topics = new();
    private ImmutableArray<Subscription<object?>> _resync = [];

    public IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler)
    {
        if (topic.IsDefault)
        {
            throw new ArgumentException("Use ChangeTopic.ForRecord or ChangeTopic.ForType.", nameof(topic));
        }

        ArgumentNullException.ThrowIfNull(handler);
        var subscription = new Subscription<EntityChange>(handler, s => Remove(topic, s));
        _topics.AddOrUpdate(topic, static (_, s) => [s], static (_, list, s) => list.Add(s), subscription);
        return subscription;
    }

    public IDisposable OnResync(Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var subscription = new Subscription<object?>(_ => handler(), s => ImmutableInterlocked.Update(ref _resync, static (list, s) => list.Remove(s), s));
        ImmutableInterlocked.Update(ref _resync, static (list, s) => list.Add(s), subscription);
        return subscription;
    }

    public Task DispatchAsync(EntityChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (string.IsNullOrWhiteSpace(change.EntityType) || string.IsNullOrWhiteSpace(change.EntityKey))
        {
            LogInvalidChange(change.EntityType ?? string.Empty);
            return Task.CompletedTask;
        }

        var record = _topics.TryGetValue(change.RecordTopic, out var r) ? r : [];
        var type = _topics.TryGetValue(change.TypeTopic, out var t) ? t : [];
        StartAll(record.AddRange(type), change, change.RecordTopic.ToString());
        return Task.CompletedTask;
    }

    public Task RaiseResyncAsync()
    {
        StartAll(_resync, null, "resync");
        return Task.CompletedTask;
    }

    // Each handler runs until its first await here; the rest continues on its own, failures logged by InvokeAsync.
    private void StartAll<T>(ImmutableArray<Subscription<T>> subscriptions, T value, string topic)
    {
        foreach (var subscription in subscriptions)
        {
            if (!subscription.IsDisposed)
            {
                _ = InvokeAsync(subscription, value, topic);
            }
        }
    }

    private async Task InvokeAsync<T>(Subscription<T> subscription, T value, string topic)
    {
        try
        {
            await subscription.Handler(value).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // One subscriber's failure must never stop delivery to the others.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogHandlerFailed(ex, topic);
        }
    }

    private void Remove(ChangeTopic topic, Subscription<EntityChange> subscription)
    {
        while (_topics.TryGetValue(topic, out var current))
        {
            var updated = current.Remove(subscription);
            if (updated.Length == current.Length)
            {
                return;
            }

            var done = updated.IsEmpty
                ? _topics.TryRemove(new KeyValuePair<ChangeTopic, ImmutableArray<Subscription<EntityChange>>>(topic, current))
                : _topics.TryUpdate(topic, updated, current);
            if (done)
            {
                return;
            }
        }
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "A change handler for {Topic} failed.")]
    private partial void LogHandlerFailed(Exception exception, string topic);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Ignored a change notice of type '{EntityType}' without a type or key.")]
    private partial void LogInvalidChange(string entityType);

    private sealed class Subscription<T>(Func<T, Task> handler, Action<Subscription<T>> remove) : IDisposable
    {
        private int _disposed;

        public Func<T, Task> Handler { get; } = handler;

        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                remove(this);
            }
        }
    }
}
