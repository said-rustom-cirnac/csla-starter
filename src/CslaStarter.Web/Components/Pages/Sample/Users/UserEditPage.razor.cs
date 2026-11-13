using CslaStarter.Business.Types;
using CslaStarter.Business.Users;
using CslaStarter.Components.Types;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Web.Components.Pages.Sample.Users;

/// <summary>A user and their roles. Saving a role change refreshes that user's open screens and caches everywhere.</summary>
public partial class UserEditPage
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> Languages =
        [new("en-CA", Messages.Language_English), new("fr-CA", Messages.Language_French)];

    private IReadOnlyList<KeyValuePair<int, string>> Roles { get; set; } = [];

    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    private Csla.IDataPortal<UserEdit> Portal { get; set; } = default!;

    [Inject]
    private Csla.IDataPortal<UserRoleTypeList> RoleTypes { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync() => Roles = TypeOptions.From(await RoleTypes.FetchAsync(), null);

    private Task<UserEdit> Load() => Portal.FetchAsync(Id);

    private static void Toggle(UserEdit user, int role, ChangeEventArgs e)
    {
        if (e.Value is true)
        {
            user.AssignRole(role);
        }
        else
        {
            user.UnassignRole(role);
        }
    }

    private void BackToList() => Navigation.NavigateTo("users");
}
