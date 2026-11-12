using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

/// <summary>The real web host, on its own test database, with the test sign-in scheme as the default.</summary>
public sealed class WebAppFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", database.RuntimeConnectionString);
        builder.UseSetting("ConnectionStrings:Owner", database.OwnerConnectionString);
        builder.UseSetting("Authentication:Mode", "MockOidc");
        builder.UseSetting("Authentication:MockOidc:Authority", "http://localhost:1/entra");
        builder.ConfigureTestServices(services => services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }
}
