using CslaStarter.Domain.Platform.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

public sealed class UserDirectoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private IUserDirectory _directory = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _directory = _db.Services.GetRequiredService<IUserDirectory>();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstSignIn_CreatesAnActiveUser_WithNoOtherRow()
    {
        var user = await _directory.EnsureAsync("t1", "o1", "Ada", Ct);
        Assert.True(user.IsActive); Assert.Equal("Ada", user.DisplayName);
        Assert.Equal(user.Id, (await _directory.EnsureAsync("t1", "o1", "Renamed in Entra", Ct)).Id);
        Assert.Equal(1, await _db.CountAsync("app_user"));
    }

    [Fact]
    public async Task SecondSignIn_KeepsTheNameEditedInTheApp()
    {
        var first = await _directory.EnsureAsync("t1", "o2", "Bea", Ct);
        await _db.ExecuteAsync("update app_user set display_name = 'Bea B.' where id = @id", first.Id);
        Assert.Equal("Bea B.", (await _directory.EnsureAsync("t1", "o2", "Bea", Ct)).DisplayName);
    }

    [Fact]
    public async Task ConcurrentFirstSignIns_CreateOneUser()
    {
        var users = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _directory.EnsureAsync("t1", "o3", "Cy", Ct)));
        Assert.Single(users.Select(u => u.Id).Distinct());
    }
}
