using CslaStarter.Core.Realtime;
using CslaStarter.Data;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Integration.Tests.Realtime;

public sealed class PresenceTests
{
    private static readonly ChangeTopic Topic = ChangeTopic.ForRecord("Project", "p-1");
    private static readonly Guid Jane = Guid.CreateVersion7();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly LoopbackTransport _transport = new();

    [Fact]
    public void Presence_JoinAndLeave_AreVisibleToWatchers()
    {
        using var tracker = Tracker();
        var changes = 0;
        using var watch = tracker.Watch(Topic, () =>
        {
            changes++;
            return Task.CompletedTask;
        });

        var joined = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-1", PresenceMode.Viewing));
        Assert.Equal(["Jane"], tracker.Present(Topic).Select(p => p.DisplayName));
        joined.Dispose();

        Assert.Empty(tracker.Present(Topic));
        Assert.Equal(2, changes);
        Assert.Equal([PresenceAction.Join, PresenceAction.Leave], _transport.Sent.Select(m => m.Action));
    }

    [Fact]
    public void Presence_SameUserTwoCircuits_ListedOnce_EditingWins()
    {
        using var tracker = Tracker();

        using var viewing = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-1", PresenceMode.Viewing));
        using var editing = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-2", PresenceMode.Editing));

        var present = Assert.Single(tracker.Present(Topic));
        Assert.Equal(PresenceMode.Editing, present.Mode);
    }

    [Fact]
    public void Presence_RejoinWithNewMode_ReplacesTheEntry_AndTheOldHandleDoesNotRemoveIt()
    {
        using var tracker = Tracker();

        var viewing = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-1", PresenceMode.Viewing));
        using var editing = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-1", PresenceMode.Editing));
        viewing.Dispose();

        Assert.Equal(PresenceMode.Editing, Assert.Single(tracker.Present(Topic)).Mode);
    }

    [Fact]
    public async Task Presence_RemoteEntryWithoutHeartbeat_ExpiresAfter90Seconds()
    {
        using var tracker = Tracker();
        await _transport.DeliverAsync("instance-b", new PresenceMessage(PresenceAction.Join, Topic.EntityType, Topic.EntityKey, new PresenceEntry(Jane, "Jane", "c-9", PresenceMode.Editing)));
        Assert.Single(tracker.Present(Topic));

        _time.Advance(TimeSpan.FromSeconds(60));
        Assert.Single(tracker.Present(Topic));
        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.Empty(tracker.Present(Topic));
    }

    [Fact]
    public async Task Presence_RemoteHeartbeat_KeepsTheEntry()
    {
        using var tracker = Tracker();
        var entry = new PresenceEntry(Jane, "Jane", "c-9", PresenceMode.Editing);
        await _transport.DeliverAsync("instance-b", new PresenceMessage(PresenceAction.Join, Topic.EntityType, Topic.EntityKey, entry));

        _time.Advance(TimeSpan.FromSeconds(60));
        await _transport.DeliverAsync("instance-b", new PresenceMessage(PresenceAction.Heartbeat, Topic.EntityType, Topic.EntityKey, entry));
        _time.Advance(TimeSpan.FromSeconds(60));

        Assert.Single(tracker.Present(Topic));
    }

    [Fact]
    public void Presence_Heartbeat_ResendsLocalEntriesEvery30Seconds()
    {
        using var tracker = Tracker();
        using var joined = tracker.Join(Topic, new PresenceEntry(Jane, "Jane", "c-1", PresenceMode.Viewing));
        _transport.Sent.Clear();

        _time.Advance(TimeSpan.FromSeconds(30));

        var heartbeat = Assert.Single(_transport.Sent);
        Assert.Equal((PresenceAction.Heartbeat, "c-1"), (heartbeat.Action, heartbeat.Entry.CircuitId));
    }

    private PresenceTracker Tracker() => new(_transport, _time, NullLogger<PresenceTracker>.Instance);

    private sealed class LoopbackTransport : IPresenceTransport
    {
        private Func<string, PresenceMessage, Task>? _received;

        public List<PresenceMessage> Sent { get; } = [];

        public Task SendAsync(IReadOnlyList<PresenceMessage> messages, CancellationToken cancellationToken)
        {
            Sent.AddRange(messages);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<string, PresenceMessage, Task> received)
        {
            _received = received;
            return new Unsubscribe(() => _received = null);
        }

        public Task DeliverAsync(string instance, PresenceMessage message) => _received!(instance, message);

        private sealed class Unsubscribe(Action action) : IDisposable
        {
            public void Dispose() => action();
        }
    }
}

public sealed class PresenceAcrossInstancesTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly ChangeTopic Topic = ChangeTopic.ForRecord("Project", "p-1");

    private TestDatabase _db = null!;
    private AppDatabase _database = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _database = AppDatabase.Create(_db.RuntimeConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DataSource.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Presence_AcrossInstances_JoinOnAVisibleOnB_LeaveRemovesIt()
    {
        var (a, aTracker) = await StartAsync();
        var (b, bTracker) = await StartAsync();
        try
        {
            var joined = aTracker.Join(Topic, new PresenceEntry(Guid.CreateVersion7(), "Jane", "c-1", PresenceMode.Editing));
            await Eventually.True(() => bTracker.Present(Topic).Count == 1);
            Assert.Equal(("Jane", PresenceMode.Editing), (bTracker.Present(Topic)[0].DisplayName, bTracker.Present(Topic)[0].Mode));

            joined.Dispose();
            await Eventually.True(() => bTracker.Present(Topic).Count == 0);
        }
        finally
        {
            aTracker.Dispose();
            bTracker.Dispose();
            await a.StopAsync(CancellationToken.None);
            await b.StopAsync(CancellationToken.None);
            a.Dispose();
            b.Dispose();
        }
    }

    private async Task<(PostgresChangeBackplane Backplane, PresenceTracker Tracker)> StartAsync()
    {
        var backplane = new PostgresChangeBackplane(
            _database.DataSource, new EntityChangeHub(NullLogger<EntityChangeHub>.Instance), TimeProvider.System, NullLogger<PostgresChangeBackplane>.Instance);
        var tracker = new PresenceTracker(backplane, TimeProvider.System, NullLogger<PresenceTracker>.Instance);
        await backplane.StartAsync(CancellationToken.None);
        await Eventually.True(() => backplane.IsListening);
        return (backplane, tracker);
    }
}
