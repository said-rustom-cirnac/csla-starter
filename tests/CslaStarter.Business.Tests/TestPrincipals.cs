using System.Globalization;
using System.Security.Claims;
using CslaStarter.Core.Security;

namespace CslaStarter.Business.Tests;

/// <summary>Signed-in principals shaped like the claims transformation's output.</summary>
public static class TestPrincipals
{
    public static readonly Guid AdminId = Guid.Parse("0193a000-0000-7000-8000-00000000000a");
    public static readonly Guid TechnicalId = Guid.Parse("0193a000-0000-7000-8000-00000000000b");
    public static readonly Guid ClientId = Guid.Parse("0193a000-0000-7000-8000-00000000000c");

    public static ClaimsPrincipal Admin => User(AdminId, "Ada Admin", 1);

    public static ClaimsPrincipal Technical => User(TechnicalId, "Tess Technical", 3);

    public static ClaimsPrincipal Client => User(ClientId, "Cleo Client", 2);

    public static ClaimsPrincipal User(Guid id, string name, params int[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, name), new(AppClaimTypes.UserId, id.ToString()), new(AppClaimTypes.Language, "en-CA") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString(CultureInfo.InvariantCulture))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }
}
