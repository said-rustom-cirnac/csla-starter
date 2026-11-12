using System.Net;

namespace CslaStarter.Integration.Tests;

public sealed class WebHostTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Anonymous_RequestToHome_IsChallenged()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignedIn_RequestToHome_Succeeds()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "ada");

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
