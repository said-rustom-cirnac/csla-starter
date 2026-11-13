using Csla.Rules;
using Csla.Testing.Rules;
using CslaStarter.Business.Rules;

namespace CslaStarter.Business.Tests.Rules;

public class HasRoleTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("3", true)]
    [InlineData("2", false)]
    public async Task HasRole_AllowsListedRoles_DeniesOthers(string role, bool expected)
    {
        using var result = await AuthorizationRuleTester
            .For(new HasRole(AuthorizationActions.EditObject, 1, 3))
            .ForType<RuleProbe>()
            .AsUser("someone", role)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.HasPermission);
    }

    [Fact]
    public async Task HasRole_Anonymous_IsDenied()
    {
        using var result = await AuthorizationRuleTester
            .For(new HasRole(AuthorizationActions.EditObject, 1))
            .ForType<RuleProbe>()
            .AsUnauthenticated()
            .ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HasPermission);
    }
}
