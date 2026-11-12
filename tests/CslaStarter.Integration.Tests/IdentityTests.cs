using System.Security.Claims;
using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Security;
using CslaStarter.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CslaStarter.Integration.Tests;

public sealed class IdentityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ClaimsTransformation_FirstSignIn_CreatesUserWithNoRoles()
    {
        var principal = await Transformation().TransformAsync(EntraPrincipal("oid-new", "Grace"));

        await using var context = _db.CreateContext();
        var user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.ObjectId == "oid-new", Ct);
        Assert.Equal("Grace", user.DisplayName);
        Assert.Empty(user.Roles);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(EntraClaims.UserIdClaim)?.Value);
        Assert.DoesNotContain(principal.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task ClaimsTransformation_AddsRoleClaimsFromDatabase()
    {
        await _db.AddUserAsync("Ada", "oid-ada", true, SystemRoles.Admin, 3);

        var principal = await Transformation().TransformAsync(EntraPrincipal("oid-ada", "Ada"));

        Assert.True(principal.IsInRole("1"));
        Assert.True(principal.IsInRole("3"));
        Assert.False(principal.IsInRole("2"));
    }

    [Fact]
    public async Task ClaimsTransformation_IsIdempotent()
    {
        await _db.AddUserAsync("Ada", "oid-ada", true, SystemRoles.Admin);
        var transformation = Transformation();

        var once = await transformation.TransformAsync(EntraPrincipal("oid-ada", "Ada"));
        var twice = await transformation.TransformAsync(once);

        Assert.Equal(once.Claims.Count(), twice.Claims.Count());
    }

    [Fact]
    public async Task ClaimsTransformation_InactiveUser_GetsNoRoles()
    {
        await _db.AddUserAsync("Ada", "oid-ada", false, SystemRoles.Admin);

        var principal = await Transformation().TransformAsync(EntraPrincipal("oid-ada", "Ada"));

        Assert.False(principal.IsInRole("1"));
        Assert.True(principal.HasClaim(EntraClaims.InactiveClaim, "true"));
    }

    [Fact]
    public async Task Revalidation_InactiveUser_IsNotValid()
    {
        var id = await _db.AddUserAsync("Ada", "oid-ada", false);

        Assert.False(await Directory().IsActiveAsync(id, Ct));
    }

    [Theory]
    [InlineData("MockOidc")]
    [InlineData("Entra")]
    public void AuthCookie_HasRequiredSettings(string mode)
    {
        using var provider = AuthServices(mode, "Development");
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("__Host-CslaStarter", cookie.Cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
    }

    [Fact]
    public void MockOidc_InProduction_RefusesToStart()
    {
        var error = Assert.Throws<InvalidOperationException>(() => AuthServices("MockOidc", "Production"));
        Assert.Contains("MockOidc", error.Message, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal EntraPrincipal(string objectId, string name) =>
        new(new ClaimsIdentity([new Claim("tid", "tenant-1"), new Claim("oid", objectId), new Claim("name", name)], "Test"));

    private UserDirectory Directory() => new(
        new AppDatabaseConnection(_db.RuntimeConnectionString),
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System,
        new SecurityEventLog(new AppDatabaseConnection(_db.RuntimeConnectionString), TimeProvider.System, NullLogger<SecurityEventLog>.Instance));

    private RoleClaimsTransformation Transformation() => new(Directory());

    private ServiceProvider AuthServices(string mode, string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = mode,
            ["Authentication:MockOidc:Authority"] = "http://localhost:8080/entra",
            ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000001",
            ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000002",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AppDatabaseConnection(_db.RuntimeConnectionString));
        services.AddCslaStarterAuthentication(configuration, new TestHostEnvironment(environment));
        return services.BuildServiceProvider();
    }
}
