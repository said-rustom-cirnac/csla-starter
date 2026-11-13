using Csla.Rules;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Security;

/// <summary>
/// Shows its content only to users with one of <see cref="Roles"/> (UserRoleType codes) and/or the CSLA permission
/// <see cref="Action"/> on <see cref="ObjectType"/>. For hiding UI only: the business objects still enforce every rule.
/// </summary>
public partial class CslaAuthorize
{
    [Parameter]
    public IReadOnlyList<int>? Roles { get; set; }

    [Parameter]
    public AuthorizationActions? Action { get; set; }

    [Parameter]
    public Type? ObjectType { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? NotAuthorized { get; set; }

    private bool Allowed =>
        CurrentUser.IsAuthenticated
        && (Roles is null || CurrentUser.HasAnyRole([.. Roles]))
        && (Action is not { } action || ObjectType is null || BusinessRules.HasPermission(ApplicationContext, action, ObjectType));
}
