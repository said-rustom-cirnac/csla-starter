using Csla.Rules;
using CslaStarter.Domain.Platform.Security;

namespace CslaStarter.Domain.Platform.Rules;

/// <summary>Allows the action for any signed-in user and refuses it for an anonymous one.</summary>
public sealed class IsSignedIn(AuthorizationActions action) : AuthorizationRule(action)
{
    protected override void Execute(IAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HasPermission = new CslaCurrentUser(context.ApplicationContext).IsSignedIn;
    }
}
