using System.Security.Claims;
using Csla.Testing;
using CslaStarter.Domain.Platform.Security;

namespace CslaStarter.Domain.Tests.Security;

public class CurrentUserTests
{
    private static readonly Guid AdaId = Guid.Parse("0193a5c0-0000-7000-8000-00000000000a");

    [Fact]
    public void SignedIn_ReadsTheAppClaims()
    {
        var identity = new ClaimsIdentity(
            [new Claim(AppClaimTypes.UserId, AdaId.ToString()), new Claim(AppClaimTypes.DisplayName, "Ada")], "Test");
        using var host = CslaTestHost.Create(t => t.AsPrincipal(new ClaimsPrincipal(identity)));

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.True(user.IsSignedIn);
        Assert.Equal(AdaId, user.UserId);
        Assert.Equal("Ada", user.DisplayName);
    }

    [Fact]
    public void Anonymous_IsNotSignedIn_AndHasNoIdOrName()
    {
        using var host = CslaTestHost.Create(t => t.AsUnauthenticated());

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.False(user.IsSignedIn);
        Assert.Null(user.UserId);
        Assert.Equal("", user.DisplayName);
    }

    [Fact]
    public void Unauthenticated_WithTheAppClaims_HasNoIdOrName()
    {
        var identity = new ClaimsIdentity(
            [new Claim(AppClaimTypes.UserId, AdaId.ToString()), new Claim(AppClaimTypes.DisplayName, "Ada")]);
        using var host = CslaTestHost.Create(t => t.AsPrincipal(new ClaimsPrincipal(identity)));

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.False(user.IsSignedIn);
        Assert.Null(user.UserId);
        Assert.Equal("", user.DisplayName);
    }

    [Fact]
    public void SignedIn_WithoutTheAppClaims_HasNoIdAndAnEmptyName()
    {
        using var host = CslaTestHost.Create(t => t.AsPrincipal(new ClaimsPrincipal(new ClaimsIdentity([], "Test"))));

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.True(user.IsSignedIn);
        Assert.Null(user.UserId);
        Assert.Equal("", user.DisplayName);
    }

    [Fact]
    public void SignedIn_WithAMalformedUserId_HasNoId()
    {
        var identity = new ClaimsIdentity(
            [new Claim(AppClaimTypes.UserId, "not-a-guid"), new Claim(AppClaimTypes.DisplayName, "Ada")], "Test");
        using var host = CslaTestHost.Create(t => t.AsPrincipal(new ClaimsPrincipal(identity)));

        var user = new CslaCurrentUser(host.ApplicationContext);

        Assert.True(user.IsSignedIn);
        Assert.Null(user.UserId);
        Assert.Equal("Ada", user.DisplayName);
    }
}
