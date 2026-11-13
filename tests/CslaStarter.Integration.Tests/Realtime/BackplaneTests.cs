using System.Text.Json;
using CslaStarter.Core.Realtime;
using CslaStarter.Data;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CslaStarter.Integration.Tests.Realtime;

/// <summary>Two app instances (A and B) sharing one database, each with its own hub and backplane.</summary>
public sealed class BackplaneTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly ChangeTopic Topic = ChangeTopic.ForRecord("Project", "p-1");

    private TestDatabase _db = null!;
    private Instance _a = null!;
    private Instance _b = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _a = await Instance.StartAsync(_db.RuntimeConnectionString);
        _b = await Instance.StartAsync(_db.RuntimeConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _a.DisposeAsync();
        await _b.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Backplane_ChangeOnA_ReachesSubscriberOnB()
    {
        var received = new List<EntityChange>();
        using var _ = _b.Hub.Subscribe(Topic, Record(received));
        var change = Change("Jane");

        await _a.Backplane.PublishAsync([change], Ct);

        await Eventually.True(() => received.Count == 1);
        Assert.Equal(change, received[0]);
    }

    [Fact]
    public async Task Backplane_OwnEcho_IsNotDispatchedTwice()
    {
        var onA = new List<EntityChange>();
        var onB = new List<EntityChange>();
        using var a = _a.Hub.Subscribe(Topic, Record(onA));
        using var b = _b.Hub.Subscribe(Topic, Record(onB));

        await _a.Backplane.PublishAsync([Change("Jane")], Ct);

        await Eventually.True(() => onB.Count == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Ct);
        Assert.Single(onA);
        Assert.Single(onB);
    }

    [Fact]
    public async Task Backplane_AfterReconnect_RaisesResync_AndReceives()
    {
        var resyncs = 0;
        var received = new List<EntityChange>();
        using var resync = _b.Hub.OnResync(() =>
        {
            Interlocked.Increment(ref resyncs);
            return Task.CompletedTask;
        });
        using var subscription = _b.Hub.Subscribe(Topic, Record(received));

        await TerminateListenersAsync();

        await Eventually.True(() => Volatile.Read(ref resyncs) >= 1, TimeSpan.FromSeconds(15));
        await Eventually.True(() => _a.Backplane.IsListening && _b.Backplane.IsListening, TimeSpan.FromSeconds(15));
        await _a.Backplane.PublishAsync([Change("Jane")], Ct);
        await Eventually.True(() => received.Count == 1);
    }

    [Fact]
    public async Task Backplane_PayloadIsSmallAndHasNoFieldData()
    {
        await using var listener = new NpgsqlConnection(_db.RuntimeConnectionString);
        await listener.OpenAsync(Ct);
        string? payload = null;
        listener.Notification += (_, e) => payload = e.Payload;
        await using (var listen = new NpgsqlCommand("LISTEN app_entity_changes", listener))
        {
            await listen.ExecuteNonQueryAsync(Ct);
        }

        await _a.Backplane.PublishAsync([Change(new string('J', 200))], Ct);
        await listener.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.NotNull(payload);
        Assert.True(payload.Length < 1000, $"payload is {payload.Length} characters");
        using var json = JsonDocument.Parse(payload);
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Subset(new HashSet<string> { "instance", "type", "key", "kind", "version", "userId", "userName", "origin" }, names);
    }

    private async Task TerminateListenersAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(postgres.AdminConnectionString) { Database = _db.Name }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname = current_database() AND query LIKE 'LISTEN%' AND pid <> pg_backend_pid()",
            connection);
        Assert.Equal(2L, await command.ExecuteScalarAsync(Ct));
    }

    private static EntityChange Change(string changedBy) =>
        new("Project", "p-1", ChangeKind.Updated, 42, Guid.CreateVersion7(), changedBy, "circuit-1");

    private static Func<EntityChange, Task> Record(List<EntityChange> received) => change =>
    {
        lock (received)
        {
            received.Add(change);
        }

        return Task.CompletedTask;
    };

    private sealed class Instance : IAsyncDisposable
    {
        private Instance(AppDatabase database)
        {
            Database = database;
            Hub = new EntityChangeHub(NullLogger<EntityChangeHub>.Instance);
            Backplane = new PostgresChangeBackplane(database.DataSource, Hub, TimeProvider.System, NullLogger<PostgresChangeBackplane>.Instance);
        }

        public AppDatabase Database { get; }

        public EntityChangeHub Hub { get; }

        public PostgresChangeBackplane Backplane { get; }

        public static async Task<Instance> StartAsync(string connectionString)
        {
            var instance = new Instance(AppDatabase.Create(connectionString));
            await instance.Backplane.StartAsync(CancellationToken.None);
            await Eventually.True(() => instance.Backplane.IsListening);
            return instance;
        }

        public async ValueTask DisposeAsync()
        {
            await Backplane.StopAsync(CancellationToken.None);
            Backplane.Dispose();
            await Database.DataSource.DisposeAsync();
        }
    }
}

/// <summary>Polls a condition instead of sleeping a fixed time.</summary>
internal static class Eventually
{
    public static async Task True(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition was not met in time.");
            }

            await Task.Delay(20);
        }
    }
}
