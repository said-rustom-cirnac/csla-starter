using System.Security.Claims;
using Csla.Testing;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Types;
using CslaStarter.Dal.Projects;
using CslaStarter.Dal.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Tests.Projects;

public class ProjectTests
{
    private readonly FakeProjectDal _dal = new();
    private readonly FakeTypeDal _types = new();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public ProjectTests()
    {
        _types.Rows["ProjectStatusType"] = [new(1, "Planned", "Planifié", true, 1), new(2, "Active", "Actif", true, 2), new(3, "On hold", "En suspens", true, 3), new(4, "Closed", "Fermé", true, 4)];
        _dal.Add(_projectId, "P-1", tasks: [new ProjectTaskDto(Guid.CreateVersion7(), "Open task", false, null, 1)], members: TestPrincipals.ClientId);
    }

    [Fact]
    public async Task ProjectEdit_CodeMustBeUnique_ShowsBusyWhileChecking()
    {
        _dal.TakenCodes.Add("TAKEN");
        _dal.CodeCheckGate = new TaskCompletionSource();
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().CreateAsync();

        project.Code = "TAKEN";
        Assert.True(project.IsPropertyBusy(ProjectEdit.CodeProperty));
        _dal.CodeCheckGate.SetResult();
        await project.WaitForIdle(TimeSpan.FromSeconds(5));

        Assert.False(project.IsPropertyBusy(ProjectEdit.CodeProperty));
        Assert.Contains(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.Code));
    }

    [Fact]
    public async Task ProjectEdit_EndBeforeStart_IsError()
    {
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.StartDate = new DateOnly(2026, 12, 1);
        project.EndDate = new DateOnly(2026, 11, 1);

        Assert.Contains(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.EndDate) && r.Severity == Csla.Rules.RuleSeverity.Error);
    }

    [Fact]
    public async Task ProjectEdit_Client_CannotReadBudget()
    {
        using var host = Host(TestPrincipals.Client);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        Assert.False(project.CanReadProperty(ProjectEdit.BudgetProperty));
        Assert.Null(project.Budget);
    }

    [Fact]
    public async Task ProjectEdit_Technical_CannotWriteBudget()
    {
        using var host = Host(TestPrincipals.Technical);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        Assert.True(project.CanReadProperty(ProjectEdit.BudgetProperty));
        Assert.False(project.CanWriteProperty(ProjectEdit.BudgetProperty));
        Assert.Throws<Csla.Security.SecurityException>(() => project.Budget = 5m);
    }

    [Fact]
    public async Task ProjectEdit_Client_CannotEdit()
    {
        using var host = Host(TestPrincipals.Client);

        Assert.False(ProjectEdit.CanEdit(host.ApplicationContext));
        Assert.False(ProjectEdit.CanCreate(host.ApplicationContext));
    }

    [Fact]
    public async Task ProjectEdit_SaveRoundTrip_SendsChildren_AndComesBackClean()
    {
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.AddTask("Second task");
        project.Name = "Renamed";
        project = await project.SaveAsync();

        Assert.Equal(["Open task", "Second task"], _dal.LastSaved!.Tasks.Select(t => t.Title));
        Assert.False(project.IsDirty);
        Assert.Equal(6u, project.Version);
        Assert.Equal(2, project.Tasks.Count);
    }

    [Fact]
    public async Task CloseProject_WithOpenTasks_Fails()
    {
        using var host = Host(TestPrincipals.Admin);

        var command = await host.GetDataPortal<CloseProjectCommand>().ExecuteAsync(_projectId);

        Assert.Equal(CloseProjectResult.HasOpenTasks, command.Result);
        Assert.False(string.IsNullOrEmpty(command.Message));
        Assert.Equal(2, _dal.Projects[_projectId].StatusCode);
    }

    [Fact]
    public async Task CloseProject_TechnicalNonMember_IsDenied()
    {
        using var host = Host(TestPrincipals.Technical);

        await Assert.ThrowsAnyAsync<Exception>(() => host.GetDataPortal<CloseProjectCommand>().ExecuteAsync(_projectId));
    }

    [Fact]
    public async Task ProjectList_ClientSeesBudgetHidden()
    {
        using var host = Host(TestPrincipals.Client);

        var list = await host.GetDataPortal<ProjectList>().FetchAsync(string.Empty, 0, "Code", false, 1, 25);

        Assert.Equal(1, list.TotalCount);
        Assert.Null(list[0].Budget);
        Assert.Equal(1, list[0].OpenTaskCount);
    }

    private CslaTestHost Host(ClaimsPrincipal principal) => CslaTestHost.Create(o => o
        .AsPrincipal(principal)
        .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IProjectDal>(_dal).AddSingleton<ITypeDal>(_types)));
}
