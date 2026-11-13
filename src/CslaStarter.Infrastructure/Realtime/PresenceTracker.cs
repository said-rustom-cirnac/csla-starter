using CslaStarter.Core.Realtime;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// Who has which record open, on this instance and the others. Local entries are announced on join and leave and
/// re-announced every 30 seconds; another instance's entries expire 90 seconds after their last announcement (so a
/// crashed instance's users disappear). One instance per process (singleton).
/// </summary>
public sealed partial class PresenceTracker : IPresenceTracker, IDisposable
{
    private static readonly TimeSpan HeartbeatEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExpireAfter = TimeSpan.FromSeconds(90);

    private readonly IPresenceTransport _transport;
    private readonly TimeProvider _time;
    private readonly ILogger<PresenceTracker> _logger;
    private readonly IDisposable _subscription;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    // Per topic: this instance's entries by circuit, and other instances' entries by (instance, circuit).
    private readonly Dictionary<ChangeTopic, Dictionary<string, PresenceEntry>> _local = [];
    private readonly Dictionary<ChangeTopic, Dictionary<(string Instance, string Circuit), (PresenceEntry Entry, DateTimeOffset SeenAt)>> _remote = [];
    private readonly Dictionary<ChangeTopic, List<Func<Task>>> _watchers = [];

    public PresenceTracker(IPresenceTransport transport, TimeProvider time, ILogger<PresenceTracker> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(time);
        _transport = transport;
        _time = time;
        _logger = logger;
        _subscription = transport.Subscribe(ReceiveAsync);
        _timer = time.CreateTimer(_ => Tick(), null, HeartbeatEvery, HeartbeatEvery);
    }

    public IDisposable Join(ChangeTopic topic, PresenceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (topic.IsDefault)
        {
            throw new ArgumentException("Use ChangeTopic.ForRecord or ChangeTopic.ForType.", nameof(topic));
        }

        lock (_gate)
        {
            Entries(_local, topic)[entry.CircuitId] = entry;
        }

        Send(PresenceAction.Join, topic, entry);
        Notify(topic);
        return new Membership(this, topic, entry);
    }

    public IReadOnlyList<PresenceEntry> Present(ChangeTopic topic)
    {
        lock (_gate)
        {
            var local = _local.TryGetValue(topic, out var mine) ? mine.Values : Enumerable.Empty<PresenceEntry>();
            var remote = _remote.TryGetValue(topic, out var theirs) ? theirs.Values.Select(r => r.Entry) : [];
            return local.Concat(remote)
                .GroupBy(e => e.UserId)
                .Select(user => user.OrderBy(e => e.Mode == PresenceMode.Editing ? 0 : 1).First())
                .OrderBy(e => e.DisplayName, StringComparer.CurrentCulture)
                .ToList();
        }
    }

    public IDisposable Watch(ChangeTopic topic, Func<Task> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (_gate)
        {
            Entries(_watchers, topic).Add(changed);
        }

        return new Callback(() =>
        {
            lock (_gate)
            {
                if (_watchers.TryGetValue(topic, out var list) && list.Remove(changed) && list.Count == 0)
                {
                    _watchers.Remove(topic);
                }
            }
        });
    }

    public void Dispose()
    {
        _timer.Dispose();
        _subscription.Dispose();
    }

    private void Leave(ChangeTopic topic, PresenceEntry entry)
    {
        lock (_gate)
        {
            // A rejoin with a new mode replaced this entry: the old handle must not remove the new one.
            if (!_local.TryGetValue(topic, out var entries) || !entries.TryGetValue(entry.CircuitId, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            entries.Remove(entry.CircuitId);
            if (entries.Count == 0)
            {
                _local.Remove(topic);
            }
        }

        Send(PresenceAction.Leave, topic, entry);
        Notify(topic);
    }

    private Task ReceiveAsync(string instance, PresenceMessage message)
    {
        var topic = message.EntityKey is null ? ChangeTopic.ForType(message.EntityType) : ChangeTopic.ForRecord(message.EntityType, message.EntityKey);
        var key = (instance, message.Entry.CircuitId);
        bool changed;
        lock (_gate)
        {
            var entries = Entries(_remote, topic);
            if (message.Action == PresenceAction.Leave)
            {
                changed = entries.Remove(key);
            }
            else
            {
                changed = !entries.TryGetValue(key, out var known) || known.Entry != message.Entry;
                entries[key] = (message.Entry, _time.GetUtcNow());
            }

            if (entries.Count == 0)
            {
                _remote.Remove(topic);
            }
        }

        if (changed)
        {
            Notify(topic);
        }

        return Task.CompletedTask;
    }

    private void Tick()
    {
        var expired = new List<ChangeTopic>();
        List<PresenceMessage> heartbeats;
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            foreach (var (topic, entries) in _remote.ToList())
            {
                foreach (var stale in entries.Where(e => now - e.Value.SeenAt >= ExpireAfter).Select(e => e.Key).ToList())
                {
                    entries.Remove(stale);
                    expired.Add(topic);
                }

                if (entries.Count == 0)
                {
                    _remote.Remove(topic);
                }
            }

            heartbeats = [.. _local.SelectMany(t => t.Value.Values.Select(e => new PresenceMessage(PresenceAction.Heartbeat, t.Key.EntityType, t.Key.EntityKey, e)))];
        }

        Send(heartbeats);
        foreach (var topic in expired.Distinct())
        {
            Notify(topic);
        }
    }

    private void Send(PresenceAction action, ChangeTopic topic, PresenceEntry entry) =>
        Send([new PresenceMessage(action, topic.EntityType, topic.EntityKey, entry)]);

    private void Send(List<PresenceMessage> messages)
    {
        if (messages.Count > 0)
        {
            _ = SendSafelyAsync(messages);
        }
    }

    private async Task SendSafelyAsync(List<PresenceMessage> messages)
    {
        try
        {
            await _transport.SendAsync(messages, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Presence is best-effort; a missed announcement is repaired by the next heartbeat.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSendFailed(ex, messages.Count);
        }
    }

    private void Notify(ChangeTopic topic)
    {
        Func<Task>[] watchers;
        lock (_gate)
        {
            watchers = _watchers.TryGetValue(topic, out var list) ? [.. list] : [];
        }

        foreach (var watcher in watchers)
        {
            _ = NotifySafelyAsync(watcher, topic);
        }
    }

    private async Task NotifySafelyAsync(Func<Task> watcher, ChangeTopic topic)
    {
        try
        {
            await watcher().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // One watcher's failure must not stop the others.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogWatcherFailed(ex, topic.ToString());
        }
    }

    private static TValue Entries<TValue>(Dictionary<ChangeTopic, TValue> map, ChangeTopic topic)
        where TValue : new()
    {
        if (!map.TryGetValue(topic, out var entries))
        {
            map[topic] = entries = new TValue();
        }

        return entries;
    }

    [LoggerMessage(EventId = 2200, Level = LogLevel.Warning, Message = "Sending {Count} presence messages failed.")]
    private partial void LogSendFailed(Exception exception, int count);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "A presence watcher for {Topic} failed.")]
    private partial void LogWatcherFailed(Exception exception, string topic);

    private sealed class Membership(PresenceTracker tracker, ChangeTopic topic, PresenceEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                tracker.Leave(topic, entry);
            }
        }
    }

    private sealed class Callback(Action dispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
