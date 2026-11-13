using System.Security.Claims;
using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Security;
using CslaStarter.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Equal("/access-denied", cookie.AccessDeniedPath);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("UAT")]
    public void MockOidc_OutsideDevelopmentAndTesting_RefusesToStart(string environment)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AuthServices("MockOidc", environment));
        Assert.Contains("MockOidc", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "1", true)]
    [InlineData(false, "1", false)]
    [InlineData(true, "", false)]
    public async Task DefaultPolicy_IsAsStrictAsFallback(bool hasAppIdentity, string inactive, bool expected)
    {
        using var provider = AuthServices("MockOidc", "Development");
        var claims = new List<Claim> { new("name", "Ada") };
        if (hasAppIdentity)
        {
            claims.Add(new Claim(EntraClaims.UserIdClaim, Guid.NewGuid().ToString()));
        }

        if (inactive.Length == 0)
        {
            claims.Add(new Claim(EntraClaims.InactiveClaim, "true"));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(user, null, await policies.GetDefaultPolicyAsync());

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(new[] { 1 }, new[] { 1 }, true, true)]
    [InlineData(new[] { 1 }, new int[0], true, false)]
    [InlineData(new[] { 1 }, new[] { 1, 3 }, true, false)]
    [InlineData(new[] { 1 }, new[] { 1 }, false, false)]
    public void Revalidation_DetectsRoleAndActiveChanges(int[] claimed, int[] current, bool active, bool expected)
    {
        var claims = claimed.Select(c => new Claim(ClaimTypes.Role, c.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));

        Assert.Equal(expected, UserRevalidatingAuthenticationStateProvider.StillMatches(principal, new DirectoryUser(Guid.NewGuid(), active, current.ToHashSet(), "en-CA")));
    }

    [Fact]
    public async Task ClaimsTransformation_LongDisplayName_IsTruncatedNotRejected()
    {
        var longName = new string('a', 256);

        var principal = await Transformation().TransformAsync(EntraPrincipal("oid-long", longName));

        Assert.NotNull(principal.FindFirst(EntraClaims.UserIdClaim));
        await using var context = _db.CreateContext();
        Assert.Equal(200, (await context.Users.SingleAsync(u => u.ObjectId == "oid-long", Ct)).DisplayName.Length);
    }

    private static ClaimsPrincipal EntraPrincipal(string objectId, string name) =>
        new(new ClaimsIdentity([new Claim("tid", "tenant-1"), new Claim("oid", objectId), new Claim("name", name)], "Test"));

    private UserDirectory Directory() => new(
        AppDatabase.Create(_db.RuntimeConnectionString),
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System,
        new SecurityEventLog(AppDatabase.Create(_db.RuntimeConnectionString), TimeProvider.System, NullLogger<SecurityEventLog>.Instance));

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
        services.AddSingleton(AppDatabase.Create(_db.RuntimeConnectionString));
        services.AddCslaStarterAuthentication(configuration, new TestHostEnvironment(environment));
        return services.BuildServiceProvider();
    }
}
