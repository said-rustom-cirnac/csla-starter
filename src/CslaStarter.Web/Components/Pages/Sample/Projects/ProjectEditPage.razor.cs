using CslaStarter.Business.Projects;
using CslaStarter.Business.Types;
using CslaStarter.Components.Services;
using CslaStarter.Components.Types;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Web.Components.Pages.Sample.Projects;

/// <summary>
/// A project with its tasks and members: the sample of an editable aggregate. Everything (permissions, rules, live
/// updates, drafts, conflicts, history) comes from the components and the business object; the page only composes.
/// </summary>
public partial class ProjectEditPage
{
    private ProjectStatusTypeList? _statuses;

    [Parameter]
    public Guid? Id { get; set; }

    [Inject]
    private Csla.IDataPortal<ProjectEdit> Portal { get; set; } = default!;

    [Inject]
    private Csla.IDataPortal<ProjectStatusTypeList> Statuses { get; set; } = default!;

    [Inject]
    private Csla.IDataPortal<CloseProjectCommand> Close { get; set; } = default!;

    [Inject]
    private IToastService Toasts { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync() => _statuses = await Statuses.FetchAsync();

    private Task<ProjectEdit> Load() => Id is { } id ? Portal.FetchAsync(id) : Portal.CreateAsync();

    private IReadOnlyList<KeyValuePair<int, string>> StatusOptions(ProjectEdit project) =>
        _statuses is null ? [] : TypeOptions.From(_statuses, project.StatusCode);

    private void OnSaved(ProjectEdit project)
    {
        if (Id is null)
        {
            Navigation.NavigateTo($"projects/{project.Id}");
        }
    }

    private async Task CloseAsync(ProjectEdit project)
    {
        var result = await Close.ExecuteAsync(project.Id);
        Toasts.Show(result.Result == CloseProjectResult.Closed ? ToastKind.Success : ToastKind.Warning, result.Message);
    }

    private void BackToList() => Navigation.NavigateTo("projects");
}
