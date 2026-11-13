using System.Text;
using CslaStarter.Core.Drafts;
using CslaStarter.Data;
using CslaStarter.Data.Drafts;
using CslaStarter.Infrastructure.Realtime;
using CslaStarter.Integration.Tests.Realtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Integration.Tests.Drafts;

public sealed class DraftStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("Protected B project notes");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 10, 42, 0, TimeSpan.Zero));
    private readonly EphemeralDataProtectionProvider _protection = new();
    private TestDatabase _db = null!;
    private AppDatabase _database = null!;
    private Guid _ada;
    private Guid _bo;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _database = AppDatabase.Create(_db.RuntimeConnectionString);
        _ada = await _db.AddUserAsync("Ada");
        _bo = await _db.AddUserAsync("Bo");
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DataSource.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Drafts_SaveAndGet_RoundTrips()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));

        var draft = await UseStore(_ada, store => store.GetAsync(Key(_ada), Ct));

        Assert.NotNull(draft);
        Assert.Equal(Plaintext, draft.Payload);
        Assert.Equal(((uint?)7, _time.GetUtcNow()), (draft.Version, draft.SavedAt));
    }

    [Fact]
    public async Task Drafts_SaveTwice_ReplacesTheDraft()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));
        _time.Advance(TimeSpan.FromMinutes(5));
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 8, [1, 2, 3], Ct));

        var draft = await UseStore(_ada, store => store.GetAsync(Key(_ada), Ct));

        Assert.Equal((uint?)8, draft!.Version);
        Assert.Equal([1, 2, 3], draft.Payload);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.Drafts.CountAsync(Ct));
    }

    [Fact]
    public async Task Drafts_PayloadIsEncryptedAtRest()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));

        await using var context = _db.CreateContext();
        var stored = (await context.Drafts.SingleAsync(Ct)).Payload;
        Assert.False(stored.AsSpan().IndexOf(Plaintext) >= 0, "the stored payload contains the plaintext");
        Assert.False(stored.AsSpan().IndexOf("project notes"u8) >= 0);
    }

    [Fact]
    public async Task Drafts_OtherUser_CannotReadOrDelete()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseStore(_bo, store => store.GetAsync(Key(_ada), Ct)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseStore(_bo, store => store.DeleteAsync(Key(_ada), Ct)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseStore(_bo, store => store.SaveAsync(Key(_ada), 1, [9], Ct)));

        Assert.Equal(Plaintext, (await UseStore(_ada, store => store.GetAsync(Key(_ada), Ct)))!.Payload);
    }

    [Fact]
    public async Task Drafts_CopiedToAnotherUser_DoNotDecrypt()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));
        await UseStore(_bo, store => store.SaveAsync(Key(_bo), 7, [0], Ct));
        await using (var context = _db.CreateContext(asOwner: true))
        {
            var adas = await context.Drafts.SingleAsync(d => d.UserId == _ada, Ct);
            var bos = await context.Drafts.SingleAsync(d => d.UserId == _bo, Ct);
            bos.Payload = adas.Payload;
            await context.SaveChangesAsync(Ct);
        }

        Assert.Null(await UseStore(_bo, store => store.GetAsync(Key(_bo), Ct)));
    }

    [Fact]
    public async Task Drafts_Expired_ReturnsNull()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));

        _time.Advance(TimeSpan.FromHours(8));

        Assert.Null(await UseStore(_ada, store => store.GetAsync(Key(_ada), Ct)));
    }

    [Fact]
    public async Task Drafts_Delete_RemovesIt()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 7, Plaintext, Ct));

        await UseStore(_ada, store => store.DeleteAsync(Key(_ada), Ct));
        await UseStore(_ada, store => store.DeleteAsync(Key(_ada), Ct));

        Assert.Null(await UseStore(_ada, store => store.GetAsync(Key(_ada), Ct)));
    }

    [Fact]
    public async Task Drafts_Cleanup_DeletesExpiredOnly()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada, "old"), 1, [1], Ct));
        _time.Advance(TimeSpan.FromHours(5));
        await UseStore(_ada, store => store.SaveAsync(Key(_ada, "recent"), 1, [2], Ct));
        _time.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));

        var deleted = await DraftStore.DeleteExpiredAsync(_database, _time, Ct);

        Assert.Equal(1, deleted);
        await using var context = _db.CreateContext();
        Assert.Equal(["recent"], await context.Drafts.Select(d => d.EntityKey).ToListAsync(Ct));
    }

    [Fact]
    public async Task DraftCleanupService_DeletesExpiredDraftsWhenItStarts()
    {
        await UseStore(_ada, store => store.SaveAsync(Key(_ada), 1, [1], Ct));
        _time.Advance(TimeSpan.FromHours(9));
        using var service = new DraftCleanupService(_database, _time, NullLogger<DraftCleanupService>.Instance);

        await service.StartAsync(Ct);

        await Eventually.True(() => DraftCountAsync().Result == 0);
        await service.StopAsync(Ct);
    }

    private async Task<int> DraftCountAsync()
    {
        await using var context = _db.CreateContext();
        return await context.Drafts.CountAsync(Ct);
    }

    private static DraftKey Key(Guid userId, string entityKey = "p-1") => new(userId, "Project", entityKey);

    private Task<T> UseStore<T>(Guid userId, Func<DraftStore, Task<T>> action) => action(Store(userId));

    private Task UseStore(Guid userId, Func<DraftStore, Task> action) => action(Store(userId));

    private DraftStore Store(Guid userId) =>
        new(_database, new FakeDataAccessContext(userId, "user"), _protection, _time, NullLogger<DraftStore>.Instance);
}
