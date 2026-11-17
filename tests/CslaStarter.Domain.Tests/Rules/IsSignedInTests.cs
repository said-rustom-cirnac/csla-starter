using System.Security.Claims;
using Csla.Rules;
using Csla.Testing;
using Csla.Testing.Rules;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Security;
using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Rules;

// Outcomes of IsSignedIn, an authorization rule (it raises no message), one theory row each:
// | Principal                               | Action     | Outcome |
// |-----------------------------------------|------------|---------|
// | signed in                               | GetObject  | allowed |
// | signed in                               | EditObject | allowed |
// | anonymous                               | GetObject  | refused |
// | anonymous                               | EditObject | refused |
// | not authenticated, carries the claims   | GetObject  | refused |
// | not authenticated, carries the claims   | EditObject | refused |
// Where it is used (UsersObjects_NeedSignIn): UserEdit for get and edit; UserList and UserInfo for get.
public class IsSignedInTests
{
    [Theory, InlineData(AuthorizationActions.GetObject), InlineData(AuthorizationActions.EditObject)]
    public async Task SignedIn_IsAllowed(AuthorizationActions action)
    {
        using var result = await AuthorizationRuleTester.For(new IsSignedIn(action)).ForType<UserEdit>()
            .AsUser("Ada").ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.True(result.HasPermission);
    }

    [Theory, InlineData(AuthorizationActions.GetObject), InlineData(AuthorizationActions.EditObject)]
    public async Task Anonymous_IsRefused(AuthorizationActions action)
    {
        using var result = await AuthorizationRuleTester.For(new IsSignedIn(action)).ForType<UserEdit>()
            .AsUnauthenticated().ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.False(result.HasPermission);
    }

    [Theory, InlineData(AuthorizationActions.GetObject), InlineData(AuthorizationActions.EditObject)]
    public async Task NotAuthenticated_WithTheAppClaims_IsRefused(AuthorizationActions action)
    {
        var identity = new ClaimsIdentity(
            [new Claim(AppClaimTypes.UserId, Guid.CreateVersion7().ToString()), new Claim(AppClaimTypes.DisplayName, "Ada")]);
        using var result = await AuthorizationRuleTester.For(new IsSignedIn(action)).ForType<UserEdit>()
            .AsPrincipal(new ClaimsPrincipal(identity)).ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.False(result.HasPermission);
    }

    [Theory]
    [InlineData(typeof(UserEdit), AuthorizationActions.GetObject)]
    [InlineData(typeof(UserEdit), AuthorizationActions.EditObject)]
    [InlineData(typeof(UserList), AuthorizationActions.GetObject)]
    [InlineData(typeof(UserInfo), AuthorizationActions.GetObject)]
    public void UsersObjects_NeedSignIn(Type type, AuthorizationActions action)
    {
        using var signedIn = CslaTestHost.Create(t => t.AsUser("Ada"));
        using var anonymous = CslaTestHost.Create(t => t.AsUnauthenticated());
        Assert.True(BusinessRules.HasPermission(signedIn.ApplicationContext, action, type));
        Assert.False(BusinessRules.HasPermission(anonymous.ApplicationContext, action, type));
    }
}
