using CslaStarter.Business.Projects;
using CslaStarter.Business.Types;
using CslaStarter.Components.Tables;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Web.Components.Pages.Sample.Projects;

/// <summary>The projects the user may see (record-level access), paged and sorted on the server, live.</summary>
public partial class ProjectListPage
{
    private ProjectStatusTypeList? _statuses;

    [Inject]
    private Csla.IDataPortal<ProjectList> Projects { get; set; } = default!;

    [Inject]
    private Csla.IDataPortal<ProjectStatusTypeList> Statuses { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync() => _statuses = await Statuses.FetchAsync();

    private Task<ProjectList> Load(TableState state) =>
        Projects.FetchAsync(state.Filter, 0, state.SortBy ?? "Code", state.Descending, state.Page, state.PageSize);

    private string StatusName(int code) => _statuses?.Find(code)?.Description ?? code.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void Open(ProjectInfo project) => Navigation.NavigateTo($"projects/{project.Id}");
}
