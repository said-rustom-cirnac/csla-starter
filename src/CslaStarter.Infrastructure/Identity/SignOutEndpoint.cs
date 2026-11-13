using System.Diagnostics;
using CslaStarter.Core.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// <c>POST /account/signout</c> (with an antiforgery token): records the sign-out, ends the app cookie and signs out of
/// Entra ID (or the mock). The session-timeout component posts here when the session expires.
/// </summary>
public static class SignOutEndpoint
{
    public const string Path = "/account/signout";

    public static IEndpointConventionBuilder MapSignOutEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Path, async (HttpContext context, ISecurityEventLog securityEvents) =>
        {
            await securityEvents.WriteAsync(
                new SecurityEvent(SecurityEventKind.SignOut, EntraClaims.UserId(context.User), EntraClaims.DisplayName(context.User), "Signed out.", Activity.Current?.TraceId.ToString()),
                context.RequestAborted);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [OpenIdConnectDefaults.AuthenticationScheme]);
        }).WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));
}
