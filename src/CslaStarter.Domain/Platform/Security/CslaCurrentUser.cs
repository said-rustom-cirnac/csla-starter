using System.Globalization;
using System.Security.Claims;
using Csla;

namespace CslaStarter.Domain.Platform.Security;

/// <summary>The current user read from CSLA's <see cref="ApplicationContext.Principal"/> and the app's claims.</summary>
public sealed class CslaCurrentUser(ApplicationContext applicationContext) : ICurrentUser
{
    private ClaimsPrincipal Principal => applicationContext.Principal;

    public bool IsSignedIn => Principal.Identity?.IsAuthenticated == true;

    public Guid? UserId => Principal.FindFirst(AppClaimTypes.UserId)?.Value is { } value
        ? Guid.Parse(value, CultureInfo.InvariantCulture)
        : null;

    public string DisplayName => Principal.FindFirst(AppClaimTypes.DisplayName)?.Value ?? "";
}
