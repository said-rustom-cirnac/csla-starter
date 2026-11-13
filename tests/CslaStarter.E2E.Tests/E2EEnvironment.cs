using System.Net;
using CslaStarter.Data;
using CslaStarter.Data.Entities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CslaStarter.E2E.Tests.E2EEnvironment))]

namespace CslaStarter.E2E.Tests;

/// <summary>
/// The whole app for a browser: PostgreSQL and the mock Entra ID in containers, the app on Kestrel over HTTPS, and
/// Chromium. Nothing starts unless E2E=1 (see this project's CLAUDE.md).
/// </summary>
public sealed class E2EEnvironment : IAsyncLifetime
{
    public const string Admin = "ada";
    public const string Technical = "bea";

    private PostgreSqlContainer? _postgres;
    private IContainer? _oidc;
    private AppFactory? _app;
    private IPlaywright? _playwright;

    public static bool Enabled => Environment.GetEnvironmentVariable("E2E") == "1";

    public IBrowser Browser { get; private set; } = default!;

    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder("postgres:18").Build();
        _oidc = new ContainerBuilder("ghcr.io/navikt/mock-oauth2-server:6.1.5")
            .WithEnvironment("SERVER_PORT", "8080")
            .WithEnvironment("JSON_CONFIG", await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "mock-oidc.json")))
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/entra/.well-known/openid-configuration").ForPort(8080)))
            .Build();
        await Task.WhenAll(_postgres.StartAsync(), _oidc.StartAsync());

        var connection = _postgres.GetConnectionString();
        await SeedAsync(connection);
        _app = new AppFactory(connection, $"http://localhost:{_oidc.GetMappedPublicPort(8080)}/entra");
        _app.UseKestrel(options => options.ListenLocalhost(0, listen => listen.UseHttps()));
        _app.StartServer();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_oidc is not null)
        {
            await _oidc.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>A new browser session signed in as <paramref name="subject"/> through the mock Entra ID.</summary>
    public async Task<IPage> SignInAsync(string subject, string path = "/projects")
    {
        var context = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, BaseURL = BaseUrl });
        var page = await context.NewPageAsync();
        await page.GotoAsync(path);
        await page.Locator("input[name=username]").FillAsync(subject);
        await page.Locator("form [type=submit]").First.ClickAsync();
        await page.WaitForURLAsync($"**{path}");
        return page;
    }

    // The schema, and the two users the tests sign in as (the mock's subject is the Entra object id).
    private static async Task SeedAsync(string connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(connection);
        await using var db = new AppDbContext(options.Options);
        await db.Database.MigrateAsync();
        foreach (var (subject, role) in new[] { (Admin, 1), (Technical, 3) })
        {
            var user = new AppUser { TenantId = "local-tenant", ObjectId = subject, DisplayName = subject };
            user.Roles.Add(new UserRole { RoleTypeCode = role });
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }

    private sealed class AppFactory(string connection, string authority) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets();
            builder.UseSetting("ConnectionStrings:App", connection);
            builder.UseSetting("ConnectionStrings:Owner", connection);
            builder.UseSetting("Authentication:Mode", "MockOidc");
            builder.UseSetting("Authentication:MockOidc:Authority", authority);
            builder.UseSetting("Authentication:MockOidc:RequireHttpsMetadata", "false");
            builder.UseSetting("Realtime:Backplane", "Postgres");
            builder.UseSetting("Session:WarningAfter", "00:00:20");
            builder.UseSetting("Session:SignOutAfter", "00:00:40");
        }
    }
}

/// <summary>Skips a test when the end-to-end environment isn't enabled.</summary>
internal static class E2E
{
    public static void Require() => Assert.SkipUnless(E2EEnvironment.Enabled, "End-to-end tests run with E2E=1 (see tests/CslaStarter.E2E.Tests/CLAUDE.md).");
}
