using CslaStarter.Domain.Platform;
using CslaStarter.Domain.Platform.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

public sealed class UserDalTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private IUserDal _users = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _users = _db.Services.GetRequiredService<IUserDal>();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_UnknownUser_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _users.GetAsync(Guid.CreateVersion7(), Ct));

    [Fact]
    public async Task List_IsOrderedByDisplayName_WithEachRowsVersion()
    {
        // Neither insertion order (Cy, Ada, Bea) nor id order (Cy, Bea, Ada) is name order.
        var cy = Guid.Parse("0193a5c0-0000-7000-8000-000000000001");
        var bea = Guid.Parse("0193a5c0-0000-7000-8000-000000000002");
        var ada = Guid.Parse("0193a5c0-0000-7000-8000-000000000003");
        await _db.ExecuteAsync(
            """
            insert into app_user (id, tenant_id, object_id, display_name, is_active)
            values (@cy, 't1', 'o1', 'Cy', true), (@ada, 't1', 'o2', 'Ada', true), (@bea, 't1', 'o3', 'Bea', true)
            """,
            cy, ada, bea);

        var users = await _users.ListAsync(Ct);

        Assert.Equal(new[] { ("Ada", ada), ("Bea", bea), ("Cy", cy) }, users.Select(u => (u.DisplayName, u.Id)));
        foreach (var user in users)
        {
            Assert.Equal(await XminAsync(user.Id), user.Version);
        }
    }

    [Fact]
    public async Task Get_KnownUser_ReturnsItsNameAndVersion()
    {
        var id = Guid.CreateVersion7();
        await _db.ExecuteAsync("insert into app_user (id, tenant_id, object_id, display_name, is_active) values (@id, 't1', 'o1', 'Ada', true)", id);

        var user = await _users.GetAsync(id, Ct);

        Assert.Equal(("Ada", await XminAsync(id)), (user.DisplayName, user.Version));
    }

    private async Task<uint> XminAsync(Guid id) =>
        (uint)await _db.ScalarAsync<long>("select xmin::text::bigint from app_user where id = @id", id);
}
