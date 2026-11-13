using CslaStarter.Core.Security;
using CslaStarter.Dal;
using CslaStarter.Dal.Users;
using CslaStarter.Data.Dal;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Integration.Tests;

public sealed class UserDalTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly RecordingInvalidator _invalidator = new();
    private readonly RecordingSecurityEventLog _securityEvents = new();
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task UserDal_UpdateRoles_ReplacesAssignments_AndTouchesRoot()
    {
        var id = await _db.AddUserAsync("Bob", "oid-bob", true, 2);
        var before = await FetchAsync(id);

        var saved = await UpdateAsync(new UserUpdateDto(id, before.Version, "Bob", true, null, [1, 3]));

        Assert.Equal([1, 3], saved.RoleCodes.Order());
        Assert.NotEqual(before.Version, saved.Version);
        Assert.Contains(("tenant-1", "oid-bob"), _invalidator.Calls);
        await using var context = _db.CreateContext();
        var roleEvents = await context.AuditEvents.Where(e => e.EntityType == "UserRole" && e.RootKey == id.ToString()).Select(e => e.Action).ToListAsync(Ct);
        Assert.Contains("Delete", roleEvents);
        Assert.Equal(3, roleEvents.Count(a => a == "Insert"));
    }

    [Fact]
    public async Task UserDal_RoleChange_WritesSecurityEvent_ButOtherEditsDoNot()
    {
        var id = await _db.AddUserAsync("Bob", "oid-bob", true, 2);
        var loaded = await FetchAsync(id);

        var renamed = await UpdateAsync(new UserUpdateDto(id, loaded.Version, "Bob B.", true, null, [2]));
        Assert.Empty(_securityEvents.Written);
        await UpdateAsync(new UserUpdateDto(id, renamed.Version, "Bob B.", true, null, [1, 2]));

        var written = Assert.Single(_securityEvents.Written);
        Assert.Equal((SecurityEventKind.RoleChanged, FakeDataAccessContext.Tester.UserId, "tester"), (written.Kind, written.UserId, written.UserName));
        Assert.Contains(id.ToString(), written.Detail, StringComparison.Ordinal);
        Assert.Contains("2 -> 1,2", written.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserDal_StaleVersion_ThrowsConcurrency()
    {
        var id = await _db.AddUserAsync("Bob", 2);
        var loaded = await FetchAsync(id);
        await UpdateAsync(new UserUpdateDto(id, loaded.Version, "Bob B.", true, null, [2]));

        await Assert.ThrowsAsync<ConcurrencyException>(() => UpdateAsync(new UserUpdateDto(id, loaded.Version, "Bob C.", true, null, [2])));
    }

    [Fact]
    public async Task UserDal_RoleOnlyChangeByOtherAdmin_MakesStaleSaveFail()
    {
        var id = await _db.AddUserAsync("Bob", 2);
        var loaded = await FetchAsync(id);
        await UpdateAsync(new UserUpdateDto(id, loaded.Version, "Bob", true, null, [2, 3]));

        await Assert.ThrowsAsync<ConcurrencyException>(() => UpdateAsync(new UserUpdateDto(id, loaded.Version, "Bob", false, null, [2])));
    }

    [Fact]
    public async Task UserDal_List_FiltersByRoleAndPages()
    {
        await _db.AddUserAsync("Ann", 2);
        await _db.AddUserAsync("Ben", 2, 3);
        await _db.AddUserAsync("Cal", 1);
        await using var context = _db.CreateContext();
        var dal = new UserDal(context, _invalidator, _securityEvents, FakeDataAccessContext.Tester);

        var clients = await dal.FetchListAsync(new UserListCriteria(RoleCode: 2, PageSize: 1), Ct);
        var search = await dal.FetchListAsync(new UserListCriteria(Search: "be"), Ct);

        Assert.Equal(2, clients.TotalCount);
        Assert.Equal("Ann", Assert.Single(clients.Items).DisplayName);
        Assert.Equal("Ben", Assert.Single(search.Items).DisplayName);
    }

    [Fact]
    public async Task UserDal_Fetch_Missing_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => FetchAsync(Guid.CreateVersion7()));

    private async Task<UserDto> FetchAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        return await new UserDal(context, _invalidator, _securityEvents, FakeDataAccessContext.Tester).FetchAsync(id, Ct);
    }

    private async Task<UserDto> UpdateAsync(UserUpdateDto user)
    {
        await using var context = _db.CreateContext();
        return await new UserDal(context, _invalidator, _securityEvents, FakeDataAccessContext.Tester).UpdateAsync(user, Ct);
    }

    private sealed class RecordingSecurityEventLog : ISecurityEventLog
    {
        public List<SecurityEvent> Written { get; } = [];

        public Task WriteAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
        {
            Written.Add(securityEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInvalidator : IUserRoleCacheInvalidator
    {
        public List<(string, string)> Calls { get; } = [];

        public void Invalidate(string tenantId, string objectId) => Calls.Add((tenantId, objectId));
    }
}
