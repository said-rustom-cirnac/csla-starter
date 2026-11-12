using CslaStarter.Dal.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;

namespace CslaStarter.Infrastructure.Identity;

public static class AuthenticationSetup
{
    public const string CookieName = "__Host-CslaStarter";
    public const string AccessDeniedPath = "/access-denied";

    /// <summary>
    /// Sign-in through Entra ID (Authentication:Mode = Entra, the default) or, for development and tests only, a
    /// mock OpenID Connect server (MockOidc, refused in Production). Roles come from our database; everything
    /// requires a signed-in, active user with an app identity unless an endpoint allows anonymous access.
    /// </summary>
    public static IServiceCollection AddCslaStarterAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var mode = configuration["Authentication:Mode"] ?? "Entra";
        switch (mode)
        {
            case "Entra":
                services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(options => configuration.Bind("AzureAd", options), ConfigureCookie);
                break;
            case "MockOidc" when !environment.IsDevelopment() && !environment.IsEnvironment("Testing"):
                throw new InvalidOperationException(
                    $"Authentication:Mode=MockOidc is for development and tests only; it is refused in the '{environment.EnvironmentName}' environment.");
            case "MockOidc":
                services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    })
                    .AddCookie(ConfigureCookie)
                    .AddOpenIdConnect(options =>
                    {
                        options.Authority = configuration["Authentication:MockOidc:Authority"];
                        options.ClientId = configuration["Authentication:MockOidc:ClientId"] ?? "csla-starter";
                        options.ClientSecret = configuration["Authentication:MockOidc:ClientSecret"] ?? "not-a-secret";
                        options.RequireHttpsMetadata = false;
                        options.MapInboundClaims = false;
                        options.ResponseType = "code";
                        options.UsePkce = true;
                        options.TokenValidationParameters.NameClaimType = "name";
                    });
                break;
            default:
                throw new InvalidOperationException($"Unknown Authentication:Mode '{mode}'. Use Entra or MockOidc.");
        }

        services.AddMemoryCache();
        services.AddSingleton<UserDirectory>();
        services.AddSingleton<IUserDirectory>(provider => provider.GetRequiredService<UserDirectory>());
        services.AddSingleton<IUserRoleCacheInvalidator>(provider => provider.GetRequiredService<UserDirectory>());
        services.AddSingleton<IClaimsTransformation, RoleClaimsTransformation>();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, UserRevalidatingAuthenticationStateProvider>();
        // Signed in, resolved to an app user (tid + oid were present), and still active. Used for endpoints with no
        // authorization metadata (deny by default) and for every [Authorize] / RequireAuthorization() as the base.
        var appUser = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(EntraClaims.UserIdClaim)
            .RequireAssertion(context => !context.User.HasClaim(EntraClaims.InactiveClaim, "true"))
            .Build();
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(appUser)
            .SetFallbackPolicy(appUser);
        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/"; // __Host- cookies must have Path=/, even when the app runs in an IIS virtual directory.
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.AccessDeniedPath = AccessDeniedPath;
    }
}
