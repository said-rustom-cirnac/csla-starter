using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// Adds the app's own identity to an Entra principal: our user id, preferred language and one role claim per
/// role code from the database. Idempotent, because ASP.NET Core may call it several times per request.
/// </summary>
public sealed class RoleClaimsTransformation(IUserDirectory directory) : IClaimsTransformation
{
    public const string AuthenticationType = "CslaStarter";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(c => c.Type == EntraClaims.UserIdClaim))
        {
            return principal;
        }

        var tenantId = EntraClaims.TenantId(principal);
        var objectId = EntraClaims.ObjectId(principal);
        if (tenantId is null || objectId is null)
        {
            return principal;
        }

        var user = await directory.GetOrProvisionAsync(tenantId, objectId, EntraClaims.DisplayName(principal), EntraClaims.Email(principal), CancellationToken.None);

        var identity = new ClaimsIdentity(AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(EntraClaims.UserIdClaim, user.Id.ToString()));
        identity.AddClaim(new Claim(EntraClaims.LanguageClaim, user.PreferredLanguage));
        if (!user.IsActive)
        {
            identity.AddClaim(new Claim(EntraClaims.InactiveClaim, "true"));
        }

        foreach (var code in user.RoleCodes.Order())
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, code.ToString(CultureInfo.InvariantCulture)));
        }

        var result = principal.Clone();
        result.AddIdentity(identity);
        return result;
    }
}
