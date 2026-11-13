using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace CslaStarter.Integration.Tests;

public sealed partial class WebHostTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

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

    [Fact]
    public async Task SignedIn_WithoutAppIdentity_IsForbidden()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = SignedInClient(factory);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoTenantHeader, "1");

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ErrorPage_KeepsSecurityHeaders()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = SignedInClient(factory);

        var response = await client.GetAsync(new Uri(WebAppFactory.ThrowPath, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.StartsWith("default-src 'self'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Theory]
    [InlineData("/access-denied")]
    [InlineData("/Error")]
    public async Task ErrorAndAccessDeniedPages_AreAnonymous(string path)
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hsts_IsAtLeastOneYear()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient();

        var hsts = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.HttpsPolicy.HstsOptions>>().Value;

        Assert.True(hsts.MaxAge >= TimeSpan.FromDays(365), $"HSTS max-age is {hsts.MaxAge}");
        Assert.True(hsts.IncludeSubDomains);
    }

    private const string CspTemplate =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}'; img-src 'self' data:; font-src 'self'; connect-src 'self'; form-action 'self' https://login.microsoftonline.com; upgrade-insecure-requests";

    [Fact]
    public async Task SecurityHeaders_ArePresentWithFreshNonce()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = SignedInClient(factory);

        var first = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
        var second = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
        var nonce = NonceOf(first);

        Assert.NotEqual(nonce, NonceOf(second));
        Assert.Equal(CspTemplate.Replace("{nonce}", nonce, StringComparison.Ordinal), Header(first, "Content-Security-Policy"));
        Assert.Equal("nosniff", Header(first, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(first, "Referrer-Policy"));
        Assert.Equal("accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()", Header(first, "Permissions-Policy"));
        Assert.Equal("same-origin", Header(first, "Cross-Origin-Opener-Policy"));
        Assert.Single(first.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task Csp_EveryInlineScriptCarriesTheNonce()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = SignedInClient(factory);

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);
        var nonce = NonceOf(response);

        Assert.Contains($"<meta name=\"csp-nonce\" content=\"{nonce}\"", html, StringComparison.Ordinal);
        foreach (Match script in ScriptTag().Matches(html))
        {
            if (!script.Value.Contains(" src=", StringComparison.Ordinal))
            {
                Assert.Contains($"nonce=\"{nonce}\"", script.Value, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task CultureSet_StoresCookieAndRedirectsLocally()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri("/culture/set?culture=fr-CA&redirectUri=%2Fprojects%3Fpage%3D2", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/projects?page=2", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Culture=c%3Dfr-CA%7Cuic%3Dfr-CA", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\t/evil.example")]
    [InlineData("/\r\n/evil.example")]
    public async Task CultureSet_RejectsOffSiteRedirect(string redirectUri)
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri($"/culture/set?culture=fr-CA&redirectUri={Uri.EscapeDataString(redirectUri)}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CultureSet_IgnoresUnsupportedCulture()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri("/culture/set?culture=de-DE&redirectUri=%2F", UriKind.Relative), Ct);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Html_LangFollowsCulture()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = SignedInClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Dfr-CA%7Cuic%3Dfr-CA");

        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Contains("<html lang=\"fr-CA\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthLive_IsAnonymousAndHealthy()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReportsHealthyDatabase()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Telemetry_IsRegistered()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient();

        Assert.NotNull(factory.Services.GetService<TracerProvider>());
    }

    private static HttpClient SignedInClient(WebAppFactory factory)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "ada");
        return client;
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : string.Empty;

    private static string NonceOf(HttpResponseMessage response) =>
        NonceInCsp().Match(Header(response, "Content-Security-Policy")).Groups[1].Value;

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceInCsp();

    [Fact]
    public async Task Csp_StylesUseNonces_NeverUnsafeInline_AndTheComponentScriptIsServed()
    {
        await using var factory = new WebAppFactory(_db);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var script = await client.GetAsync("/_content/CslaStarter.Components/CslaStarter.Components.lib.module.js", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, script.StatusCode);
        var csp = string.Join(' ', script.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("style-src 'self' 'nonce-", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
    }

    [GeneratedRegex("<script[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();
}
