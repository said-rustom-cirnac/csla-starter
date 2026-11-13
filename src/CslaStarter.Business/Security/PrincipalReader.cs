using System.Globalization;
using System.Security.Claims;
using CslaStarter.Core.Security;

namespace CslaStarter.Business.Security;

/// <summary>Reads the app's claims from a principal (set by the claims transformation at sign-in).</summary>
internal static class PrincipalReader
{
    public static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(AppClaimTypes.UserId)?.Value, out var id) ? id : null;

    public static IReadOnlySet<int> RoleCodes(ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypes.Role)
            .Select(c => int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : (int?)null)
            .OfType<int>()
            .ToHashSet();

    public static string DisplayName(ClaimsPrincipal principal) =>
        principal.Identity?.Name ?? principal.FindFirst("name")?.Value ?? string.Empty;
}
