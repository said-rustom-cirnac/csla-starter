using System.Globalization;
using Csla.Core;
using Csla.Rules;

namespace CslaStarter.Business.Rules;

/// <summary>
/// Allows the action when the user holds any of the role codes (UserRoleType constants). CSLA evaluates only the
/// first authorization rule per (property, action), so list every allowed role in a single rule.
/// </summary>
public sealed class HasRole : AuthorizationRule
{
    public HasRole(AuthorizationActions action, params int[] roleCodes)
        : base(action) => RoleCodes = roleCodes ?? throw new ArgumentNullException(nameof(roleCodes));

    public HasRole(AuthorizationActions action, IMemberInfo element, params int[] roleCodes)
        : base(action, element) => RoleCodes = roleCodes ?? throw new ArgumentNullException(nameof(roleCodes));

    public IReadOnlyList<int> RoleCodes { get; }

    protected override void Execute(IAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var user = context.ApplicationContext.User;
        context.HasPermission = user.Identity?.IsAuthenticated == true
            && RoleCodes.Any(code => user.IsInRole(code.ToString(CultureInfo.InvariantCulture)));
    }
}
