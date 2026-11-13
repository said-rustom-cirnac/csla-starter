using System.Net;

namespace CslaStarter.Integration.Tests;

public sealed class SignOutTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task SignOut_WithoutAntiforgeryToken_IsRejected()
    {
        await _db.AddUserAsync("Ada", "ada", true, 1);
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "ada");

        using var response = await client.PostAsync("/account/signout", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignOut_IsPostOnly()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "ada");

        using var response = await client.GetAsync("/account/signout", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
