using CslaStarter.Business.Users;
using CslaStarter.Components.Tables;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Web.Components.Pages.Sample.Users;

/// <summary>The users (Admin and Technical), paged on the server.</summary>
public partial class UserListPage
{
    [Inject]
    private Csla.IDataPortal<UserList> Users { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private Task<UserList> Load(TableState state) => Users.FetchAsync(state.Filter, 0, state.Page, state.PageSize);

    private void Open(UserInfo user) => Navigation.NavigateTo($"users/{user.Id}");
}
