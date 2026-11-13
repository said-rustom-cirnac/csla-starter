using CslaStarter.Core.Realtime;
using CslaStarter.Data;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CslaStarter.Integration.Tests.Realtime;

public sealed class EntityChangeHubTests
{
    private readonly EntityChangeHub _hub = new(NullLogger<EntityChangeHub>.Instance);

    [Fact]
    public async Task Hub_RecordSubscriber_GetsOnlyItsRecord()
    {
        var received = new List<EntityChange>();
        using var _ = _hub.Subscribe(ChangeTopic.ForRecord("Project", "a"), Record(received));

        await _hub.DispatchAsync(Change("Project", "a"));
        await _hub.DispatchAsync(Change("Project", "b"));

        Assert.Equal(["a"], received.Select(c => c.EntityKey));
    }

    [Fact]
    public async Task Hub_TypeSubscriber_GetsEveryRecordOfItsType()
    {
        var received = new List<EntityChange>();
        using var _ = _hub.Subscribe(ChangeTopic.ForType("Project"), Record(received));

        await _hub.DispatchAsync(Change("Project", "a"));
        await _hub.DispatchAsync(Change("Project", "b"));
        await _hub.DispatchAsync(Change("User", "a"));

        Assert.Equal(["a", "b"], received.Select(c => c.EntityKey));
    }

    [Fact]
    public async Task Hub_UnrelatedSubscribers_AreNotCalled()
    {
        var calls = 0;
        var subscriptions = Enumerable.Range(0, 1000)
            .Select(i => _hub.Subscribe(ChangeTopic.ForRecord("Project", $"other-{i}"), _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            }))
            .Append(_hub.Subscribe(ChangeTopic.ForType("User"), _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            }))
            .ToList();

        await _hub.DispatchAsync(Change("Project", "a"));

        Assert.Equal(0, calls);
        subscriptions.ForEach(s => s.Dispose());
    }

    [Fact]
    public async Task Hub_DisposedSubscription_StopsReceiving()
    {
        var received = new List<EntityChange>();
        var subscription = _hub.Subscribe(ChangeTopic.ForRecord("Project", "a"), Record(received));

        await _hub.DispatchAsync(Change("Project", "a"));
        subscription.Dispose();
        subscription.Dispose();
        await _hub.DispatchAsync(Change("Project", "a"));

        Assert.Single(received);
    }

    [Fact]
    public async Task Hub_FailingHandler_DoesNotStopOthers()
    {
        var received = new List<EntityChange>();
        var topic = ChangeTopic.ForRecord("Project", "a");
        using var failing = _hub.Subscribe(topic, _ => throw new InvalidOperationException("boom"));
        using var faulted = _hub.Subscribe(topic, _ => Task.FromException(new InvalidOperationException("boom")));
        using var working = _hub.Subscribe(topic, Record(received));

        await _hub.DispatchAsync(Change("Project", "a"));

        Assert.Single(received);
    }

    [Fact]
    public async Task Hub_Resync_ReachesResyncHandlers()
    {
        var calls = 0;
        var subscription = _hub.OnResync(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await _hub.RaiseResyncAsync();
        subscription.Dispose();
        await _hub.RaiseResyncAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Topic_RequiresTypeAndKey()
    {
        Assert.Throws<ArgumentException>(() => ChangeTopic.ForType(" "));
        Assert.Throws<ArgumentException>(() => ChangeTopic.ForRecord("Project", ""));
        Assert.Throws<ArgumentException>(() => _hub.Subscribe(default, _ => Task.CompletedTask));
    }

    private static EntityChange Change(string type, string key) =>
        new(type, key, ChangeKind.Updated, 7, Guid.NewGuid(), "Jane", "circuit-1");

    private static Func<EntityChange, Task> Record(List<EntityChange> received) => change =>
    {
        lock (received)
        {
            received.Add(change);
        }

        return Task.CompletedTask;
    };
}

public sealed class RealtimeSetupTests
{
    [Fact]
    public void Setup_InMemory_RegistersOneHubAndTheInMemoryNotifier()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Realtime:Backplane"] = "InMemory" })
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(AppDatabase.Create("Host=localhost;Database=unused"))
            .AddCslaStarterRealtime(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.Same(provider.GetRequiredService<IEntityChangeHub>(), provider.GetRequiredService<IEntityChangeHub>());
        Assert.IsType<InMemoryChangeNotifier>(provider.GetRequiredService<IEntityChangeNotifier>());
    }

    [Fact]
    public void Setup_UnknownBackplane_FailsAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Realtime:Backplane"] = "Redis" })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCslaStarterRealtime(configuration));
    }
}
