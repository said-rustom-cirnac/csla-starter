using System.Security.Claims;
using Csla.Testing;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Types;
using CslaStarter.Core.Resources;
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

        await Denied.Assert(() => host.GetDataPortal<CloseProjectCommand>().ExecuteAsync(_projectId));
    }

    [Fact]
    public async Task CloseProject_PassesTheVersionItChecked()
    {
        _dal.Add(_projectId, "P-1", tasks: [new ProjectTaskDto(Guid.CreateVersion7(), "Done task", true, null, 1)]);
        using var host = Host(TestPrincipals.Admin);

        var command = await host.GetDataPortal<CloseProjectCommand>().ExecuteAsync(_projectId);

        Assert.Equal((CloseProjectResult.Closed, (uint?)5), (command.Result, _dal.ClosedWithVersion));
    }

    [Fact]
    public async Task ProjectEdit_CodeCheckFails_BreaksTheRuleInsteadOfCrashing()
    {
        _dal.CodeCheckFailure = new TimeoutException("database timeout");
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().CreateAsync();

        project.Code = "NEW-1";
        await project.WaitForIdle(TimeSpan.FromSeconds(5));

        Assert.False(project.IsBusy);
        var broken = Assert.Single(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.Code));
        Assert.Equal(Format(Messages.Common_CheckFailed), broken.Description);
    }

    [Fact]
    public async Task ProjectEdit_FetchAndSave_DoNotRunDatabaseRulesOnTheServer()
    {
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);
        Assert.Equal(0, _dal.CodeChecks);

        project.Code = "P-2";
        await project.WaitForIdle(TimeSpan.FromSeconds(5));
        project = await project.SaveAsync();

        Assert.Equal((1, "P-2"), (_dal.CodeChecks, project.Code));
    }

    [Fact]
    public async Task ProjectEdit_DuplicateCodeOnSave_IsAValidationError()
    {
        _dal.DuplicateCodeOnSave = true;
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);
        project.Name = "Renamed";

        var error = await Assert.ThrowsAsync<Csla.DataPortalException>(() => project.SaveAsync());

        var validation = Assert.IsType<Csla.Rules.ValidationException>(error.BusinessException);
        Assert.Equal(Format(Messages.Project_CodeTaken), validation.Message);
    }

    [Fact]
    public async Task ProjectEdit_ClosingByEdit_IsRejected()
    {
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.StatusCode = ProjectStatusType.Closed;

        Assert.Contains(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.StatusCode) && r.Severity == Csla.Rules.RuleSeverity.Error);
        Assert.False(project.IsSavable);
    }

    [Fact]
    public async Task ProjectEdit_AlreadyClosed_StaysValid()
    {
        _dal.Add(_projectId, "P-1", status: ProjectStatusType.Closed);
        using var host = Host(TestPrincipals.Admin);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.Name = "Renamed";

        Assert.DoesNotContain(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.StatusCode));
        Assert.True(project.IsSavable);
    }

    [Fact]
    public async Task ProjectEdit_RetiredStatus_IsAllowedOnlyAsTheOriginalValue()
    {
        _types.Rows["ProjectStatusType"] = [new(1, "Planned", "Planifié", true, 1), new(2, "Active", "Actif", true, 2), new(3, "On hold", "En suspens", false, 3), new(4, "Closed", "Fermé", true, 4)];
        _dal.Add(_projectId, "P-1", status: 3);
        using var host = Host(TestPrincipals.Admin);
        var onHold = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        onHold.StatusCode = 2;
        onHold.StatusCode = 3;
        await onHold.WaitForIdle(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(onHold.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.StatusCode));

        var other = await host.GetDataPortal<ProjectEdit>().CreateAsync();
        other.StatusCode = 3;
        await other.WaitForIdle(TimeSpan.FromSeconds(5));
        Assert.Contains(other.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.StatusCode));
    }

    [Fact]
    public async Task ProjectEdit_TechnicalChangesMembersDirectly_SaveIsDenied()
    {
        using var host = Host(TestPrincipals.Technical);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.Members.Remove(project.Members[0]);

        await Denied.Assert(() => project.SaveAsync());
        Assert.Equal([TestPrincipals.ClientId], _dal.Projects[_projectId].Members.Select(m => m.UserId));
    }

    [Fact]
    public async Task ProjectEdit_Client_CannotProbeOtherProjectsCodes()
    {
        _dal.TakenCodes.Add("HIDDEN");
        using var host = Host(TestPrincipals.Client);
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);

        project.Code = "HIDDEN";
        await project.WaitForIdle(TimeSpan.FromSeconds(5));

        Assert.Equal(0, _dal.CodeChecks);
        Assert.DoesNotContain(project.BrokenRulesCollection, r => r.Property == nameof(ProjectEdit.Code));
    }

    [Fact]
    public async Task ProjectCodeExistsCommand_Client_IsDenied()
    {
        using var host = Host(TestPrincipals.Client);

        await Denied.Assert(() => host.GetDataPortal<ProjectCodeExistsCommand>().ExecuteAsync("HIDDEN", Guid.Empty));
        Assert.Equal(0, _dal.CodeChecks);
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

    private static string Format(string message) => message.Replace("{0}", ProjectEdit.CodeProperty.FriendlyName, StringComparison.Ordinal);

    private CslaTestHost Host(ClaimsPrincipal principal) => CslaTestHost.Create(o => o
        .AsPrincipal(principal)
        .ConfigureCsla(csla => csla.UseCslaStarterRules())
        .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IProjectDal>(_dal).AddSingleton<ITypeDal>(_types)));
}
