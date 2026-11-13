using System.Security.Claims;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Forms;
using CslaStarter.Dal.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Forms;

public sealed class AppViewModelTests
{
    private static readonly Guid ProjectId = Guid.CreateVersion7();

    [Fact]
    public async Task ViewModel_Cancel_RestoresOriginalValues_IncludingRemovedChildren()
    {
        using var context = Context(TestPrincipals.Admin);
        var viewModel = await LoadAsync(context);

        viewModel.Model!.Name = "Changed";
        viewModel.Model!.Tasks.RemoveAt(0);
        Assert.True(viewModel.IsDirty);
        await viewModel.CancelAsync();

        Assert.Equal(("P-1", 1), (viewModel.Model!.Name, viewModel.Model!.Tasks.Count));
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task ViewModel_BusyChanged_FiresWhileAsyncRuleRuns()
    {
        using var context = Context(TestPrincipals.Admin);
        context.Projects.CodeCheckGate = new TaskCompletionSource();
        var viewModel = await LoadAsync(context);
        var changes = 0;
        viewModel.BusyChanged += () => changes++;

        viewModel.Model!.Code = "P-2";
        Assert.True(viewModel.Model!.IsBusy);
        Assert.False(viewModel.IsSavable);
        context.Projects.CodeCheckGate.SetResult();
        await viewModel.Model!.WaitForIdle(TimeSpan.FromSeconds(5));

        Assert.True(changes >= 2, $"BusyChanged fired {changes} times");
        Assert.True(viewModel.IsSavable);
    }

    [Fact]
    public async Task ViewModel_IsSavable_FalseWhenInvalid()
    {
        using var context = Context(TestPrincipals.Admin);
        var viewModel = await LoadAsync(context);

        viewModel.Model!.Name = string.Empty;

        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.IsSavable);
    }

    public static TheoryData<string, bool, bool, bool> Permissions() => new()
    {
        { "admin", true, true, true },
        { "technical", true, false, true },
        { "client", false, false, false },
    };

    [Theory]
    [MemberData(nameof(Permissions))]
    public async Task ViewModel_Permissions_PerObject(string role, bool edit, bool delete, bool create)
    {
        using var context = Context(role switch { "admin" => TestPrincipals.Admin, "technical" => TestPrincipals.Technical, _ => TestPrincipals.Client });
        var viewModel = await LoadAsync(context);

        Assert.Equal((edit, delete, create), (viewModel.CanEdit, viewModel.CanDelete, viewModel.CanCreate));
    }

    private static ComponentTestContext Context(ClaimsPrincipal user)
    {
        var context = new ComponentTestContext(user);
        context.Projects.Add(ProjectId, "P-1", tasks: [new ProjectTaskDto(Guid.CreateVersion7(), "Open task", false, null, 1)], members: TestPrincipals.ClientId);
        return context;
    }

    private static async Task<AppViewModel<ProjectEdit>> LoadAsync(ComponentTestContext context)
    {
        var viewModel = new AppViewModel<ProjectEdit>(context.Services.GetRequiredService<ApplicationContext>());
        var portal = context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        await viewModel.RefreshAsync(async () => await portal.FetchAsync(ProjectId));
        return viewModel;
    }
}
