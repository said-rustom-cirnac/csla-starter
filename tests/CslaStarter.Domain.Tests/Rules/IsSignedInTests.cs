using Csla.Rules;
using Csla.Testing;
using Csla.Testing.Rules;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Rules;

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
