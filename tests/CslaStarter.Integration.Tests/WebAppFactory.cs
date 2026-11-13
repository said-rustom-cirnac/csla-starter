using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

/// <summary>The real web host, on its own test database, with the test sign-in scheme as the default.</summary>
public sealed class WebAppFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    /// <summary>A request to this path throws, to exercise the exception handler.</summary>
    public const string ThrowPath = "/__test/throw";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Same DI checks as Development: a singleton must never capture a scoped service.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        // Serve the component library's static assets (_content/...) as Development and a published app do.
        builder.UseStaticWebAssets();
        builder.UseSetting("ConnectionStrings:App", database.RuntimeConnectionString);
        builder.UseSetting("ConnectionStrings:Owner", database.OwnerConnectionString);
        builder.UseSetting("Authentication:Mode", "MockOidc");
        builder.UseSetting("Authentication:MockOidc:Authority", "http://localhost:1/entra");
        builder.ConfigureTestServices(services => services
            .AddSingleton<IStartupFilter, ThrowingPathStartupFilter>()
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }
}

/// <summary>Adds a path that throws, after the app's own exception handler, so tests can see error responses.</summary>
internal sealed class ThrowingPathStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use((context, nextMiddleware) => context.Request.Path == WebAppFactory.ThrowPath
            ? throw new InvalidOperationException("Test failure for the exception handler.")
            : nextMiddleware(context));
    };
}
