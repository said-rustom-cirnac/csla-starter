using System.Globalization;
using System.Security.Claims;
using Csla;

namespace CslaStarter.Domain.Platform.Security;

/// <summary>
/// The current user read from CSLA's <see cref="ApplicationContext.Principal"/> and the app's claims. The claims
/// count only on an authenticated principal; a missing or malformed claim reads as no id or an empty name, never
/// as an exception.
/// </summary>
public sealed class CslaCurrentUser(ApplicationContext applicationContext) : ICurrentUser
{
    private ClaimsPrincipal Principal => applicationContext.Principal;

    public bool IsSignedIn => Principal.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        IsSignedIn && Guid.TryParse(Claim(AppClaimTypes.UserId), CultureInfo.InvariantCulture, out var id) ? id : null;

    public string DisplayName => IsSignedIn ? Claim(AppClaimTypes.DisplayName) ?? "" : "";

    private string? Claim(string type) => Principal.FindFirst(type)?.Value;
}
