using Csla.Rules;

namespace CslaStarter.Business.Rules;

/// <summary>Allows the action for any signed-in user, whatever their roles (for example, editing their own profile).</summary>
public sealed class IsSignedIn(AuthorizationActions action) : AuthorizationRule(action)
{
    protected override void Execute(IAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HasPermission = context.ApplicationContext.User.Identity?.IsAuthenticated == true;
    }
}
