using System.Security.Claims;
using Csla;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Security;

namespace CslaStarter.Business.Security;

/// <summary>
/// The signed-in user from CSLA's <see cref="ApplicationContext"/>: the circuit's user in Blazor, the caller's
/// principal inside the data portal, the request's user over HTTP.
/// </summary>
public sealed class CslaCurrentUser(ApplicationContext applicationContext) : ICurrentUser
{
    private ClaimsPrincipal Principal => applicationContext.User as ClaimsPrincipal ?? new ClaimsPrincipal();

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public Guid? UserId => PrincipalReader.UserId(Principal);

    public string DisplayName => PrincipalReader.DisplayName(Principal);

    public IReadOnlySet<int> RoleCodes => PrincipalReader.RoleCodes(Principal);

    public string PreferredLanguage => Principal.FindFirst(AppClaimTypes.Language)?.Value ?? SupportedCultures.English;

    public bool HasRole(int roleCode) => RoleCodes.Contains(roleCode);

    public bool HasAnyRole(params int[] roleCodes)
    {
        ArgumentNullException.ThrowIfNull(roleCodes);
        var held = RoleCodes;
        return roleCodes.Any(held.Contains);
    }
}
