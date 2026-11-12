using System.Security.Claims;
using Csla.Testing;
using CslaStarter.Business.Security;
using CslaStarter.Core.Security;

namespace CslaStarter.Business.Tests.Security;

public class CurrentUserTests
{
    private static readonly Guid AdaId = Guid.Parse("0193a000-0000-7000-8000-0000000000ad");

    [Fact]
    public void CurrentUser_ReadsRolesAndIdFromPrincipal()
    {
        using var host = CslaTestHost.Create(o => o.AsPrincipal(Principal(AdaId, "Ada", "fr-CA", 1, 3)));

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.True(user.IsAuthenticated);
        Assert.Equal(AdaId, user.UserId);
        Assert.Equal("Ada", user.DisplayName);
        Assert.Equal("fr-CA", user.PreferredLanguage);
        Assert.True(user.HasRole(1));
        Assert.True(user.HasAnyRole(2, 3));
        Assert.False(user.HasRole(2));
        Assert.Equal(new HashSet<int> { 1, 3 }, user.RoleCodes);
    }

    [Fact]
    public void CurrentUser_Anonymous_HasNoRoles()
    {
        using var host = CslaTestHost.Create(o => o.AsUnauthenticated());

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Empty(user.RoleCodes);
        Assert.False(user.HasAnyRole(1, 2, 3));
    }

    [Fact]
    public void DataAccessContext_UsesPrincipalNameIdAndRoles()
    {
        using var host = CslaTestHost.Create(o => o.AsPrincipal(Principal(AdaId, "Ada", "en-CA", 3)));

        var access = new CslaDataAccessContext(host.ApplicationContext);

        Assert.Equal(AdaId, access.UserId);
        Assert.Equal("Ada", access.UserName);
        Assert.Equal(new HashSet<int> { 3 }, access.RoleCodes);
    }

    private static ClaimsPrincipal Principal(Guid id, string name, string language, params int[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(AppClaimTypes.UserId, id.ToString()),
            new(AppClaimTypes.Language, language),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }
}
