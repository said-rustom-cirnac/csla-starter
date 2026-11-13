using System.Net;

namespace CslaStarter.Integration.Tests;

/// <summary>
/// Every sample route is served (the page itself renders over the circuit, with prerendering off: its behaviour is
/// tested with bUnit in Components.Tests), and none is reachable anonymously.
/// </summary>
public sealed class SamplePagesTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public static TheoryData<string> Routes() => new()
    {
        "/projects",
        "/projects/new",
        $"/projects/{Guid.CreateVersion7()}",
        "/users",
        $"/users/{Guid.CreateVersion7()}",
        "/profile",
    };

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task SamplePage_SignedIn_IsServed(string route)
    {
        await _db.AddUserAsync("Ada", "ada", true, 1);
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "ada");

        using var response = await client.GetAsync(route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task SamplePage_Anonymous_IsChallenged(string route)
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await client.GetAsync(route, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
