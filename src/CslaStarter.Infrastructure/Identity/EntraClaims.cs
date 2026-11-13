using System.Security.Claims;
using CslaStarter.Core.Security;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// Reads Entra ID claims whether or not inbound claim mapping is on, and names the claims this app adds.
/// Users are identified by tenant id + object id, never by name or email.
/// </summary>
public static class EntraClaims
{
    public const string UserIdClaim = AppClaimTypes.UserId;
    public const string LanguageClaim = AppClaimTypes.Language;
    public const string InactiveClaim = AppClaimTypes.Inactive;

    public static string? TenantId(ClaimsPrincipal principal) =>
        First(principal, "tid", "http://schemas.microsoft.com/identity/claims/tenantid");

    public static string? ObjectId(ClaimsPrincipal principal) =>
        First(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");

    public static string DisplayName(ClaimsPrincipal principal) =>
        First(principal, "name", ClaimTypes.Name, "preferred_username") ?? "Unknown user";

    public static string? Email(ClaimsPrincipal principal) =>
        First(principal, "email", ClaimTypes.Email, "preferred_username");

    public static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(UserIdClaim)?.Value, out var id) ? id : null;

    private static string? First(ClaimsPrincipal principal, params string[] types) =>
        types.Select(t => principal.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
