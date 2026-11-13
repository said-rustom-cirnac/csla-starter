using CslaStarter.Data.Dal;

namespace CslaStarter.Integration.Tests;

public sealed class TypeDalTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        TypeDal.ResetCache();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task TypeDal_FetchesSeededUserRoles()
    {
        await using var context = _db.CreateContext();

        var roles = await new TypeDal(context).FetchAsync("UserRoleType", TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], roles.Select(r => r.Code).Order());
        Assert.Contains(roles, r => r is { Code: 1, DescriptionEn: "Administrator", DescriptionFr: "Administrateur", IsActive: true });
    }

    [Fact]
    public async Task TypeDal_UnknownType_Throws()
    {
        await using var context = _db.CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new TypeDal(context).FetchAsync("NoSuchType", TestContext.Current.CancellationToken));
    }
}
