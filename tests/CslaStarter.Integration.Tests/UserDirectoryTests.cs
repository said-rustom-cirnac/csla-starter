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

    // Entra allows display names of up to 256 characters; the column holds 200.
    [Fact]
    public async Task LongName_SignsIn_StoredAsItsFirst200Characters()
    {
        var name = string.Concat(Enumerable.Range(0, 256).Select(i => (char)('a' + (i % 26))));
        var user = await _directory.EnsureAsync("t1", "o4", name, Ct);
        Assert.Equal(name[..200], user.DisplayName);
        Assert.Equal(name[..200], await StoredNameAsync(user.Id));
    }

    [Fact]
    public async Task LongName_IsNeverCutInsideACharacter()
    {
        var name = new string('a', 199) + "😀" + "bbb"; // the emoji is two UTF-16 units, the 200th and 201st
        var user = await _directory.EnsureAsync("t1", "o5", name, Ct);
        Assert.Equal(new string('a', 199), await StoredNameAsync(user.Id));
    }

    [Fact]
    public async Task Name_WithSurroundingSpaces_IsStoredTrimmed()
    {
        var user = await _directory.EnsureAsync("t1", "o6", "  Dee  ", Ct);
        Assert.Equal("Dee", await StoredNameAsync(user.Id));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("objectId")]
    public async Task OverlongKey_Throws_NamingTheArgument(string argument)
    {
        var tooLong = new string('x', 101);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => argument == "tenantId"
            ? _directory.EnsureAsync(tooLong, "o7", "Eve", Ct)
            : _directory.EnsureAsync("t1", tooLong, "Eve", Ct));
        Assert.Equal(argument, error.ParamName);
        Assert.Equal(0, await _db.CountAsync("app_user"));
    }

    private Task<string?> StoredNameAsync(Guid id) =>
        _db.ScalarAsync<string>("select display_name from app_user where id = @id", id);
}
