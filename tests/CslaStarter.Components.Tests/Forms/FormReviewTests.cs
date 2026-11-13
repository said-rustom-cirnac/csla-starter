using Bunit;
using Csla;
using CslaStarter.Business.Drafts;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Drafts;
using CslaStarter.Components.Forms;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Services;
using CslaStarter.Components.Tests.Buttons;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Forms;

/// <summary>Form behaviours that break in realistic use: lost access, failed deletes, restored drafts, leaving.</summary>
public sealed class FormReviewTests : IDisposable
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public FormReviewTests() => _context.Projects.Add(_projectId, "P-1", members: TestPrincipals.ClientId);

    public void Dispose() => _context.Dispose();

    private static DraftKey Key(Guid user, string key) => new(user, "Project", key);

    [Fact]
    public async Task Flush_ReleasesTheNavigationGuard_BeforeTheReload()
    {
        var host = Render<LiveFormHost>();
        await Input(host, "Name", "Mine");
        Assert.True(host.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation);

        await host.InvokeAsync(() => _context.Services.GetRequiredService<DraftFlushRegistry>().FlushAllAsync());

        Assert.False(host.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation);
    }

    [Fact]
    public async Task AccessLost_AfterTheRecordNotice_ClosesCleanly()
    {
        Use(TestPrincipals.Client);
        var host = Render<LiveFormHost>();

        _context.Projects.Projects.Remove(_projectId);
        await _context.Hub.DispatchAsync(new EntityChange("Project", _projectId.ToString(), ChangeKind.Updated, 6, TestPrincipals.AdminId, "Ada", "other"));
        await _context.Hub.DispatchAsync(EntityChange.UserAccess(TestPrincipals.ClientId, TestPrincipals.AdminId, "Ada", "other"));

        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.ClosedCount));
        Assert.DoesNotContain(_context.Services.GetRequiredService<IToastService>().Toasts, t => t.Kind == ToastKind.Error);
        Assert.Empty(host.FindAll("gcds-input"));
    }

    [Fact]
    public async Task FailedDelete_LeavesTheObjectUndeleted()
    {
        _context.Projects.DeleteFailure = new InvalidOperationException("database down");
        var host = Render<ButtonFormHost>();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;

        Assert.False(await host.InvokeAsync(form.DeleteAsync));
        await Input(host, "Name", "Renamed");
        Assert.True(await host.InvokeAsync(form.SaveAsync));

        Assert.True(_context.Projects.Projects.ContainsKey(_projectId));
        Assert.Equal("Renamed", _context.Projects.Projects[_projectId].Name);
    }

    [Fact]
    public async Task RestoredDraft_SavesFirstTime_CancelReverts_AndKeepsTheirChanges()
    {
        await StoreDraftThroughTheFormAsync(description: "My description");
        _context.Projects.Projects[_projectId] = _context.Projects.Projects[_projectId] with { Name = "Jane's name", Version = 6 };
        var host = Render<LiveFormHost>();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;

        await Click(host, "[data-csla-draft] gcds-button[data-action=restore]");

        host.WaitForAssertion(() => Assert.Equal("My description", form.ViewModel.Model!.Description));
        Assert.Equal(("Jane's name", 6u), (form.ViewModel.Model!.Name, form.ViewModel.Model.Version));
        await host.InvokeAsync(form.CancelAsync);
        Assert.True(string.IsNullOrEmpty(form.ViewModel.Model!.Description));

        await Click(host, "gcds-input[name=Description]", "ongcdsinput", "Again");
        Assert.True(await host.InvokeAsync(form.SaveAsync));
        Assert.Equal(("Jane's name", "Again"), (_context.Projects.LastSaved!.Name, _context.Projects.LastSaved.Description));
    }

    [Fact]
    public async Task NewRecordDraft_IsOfferedBack()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var first = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.CreateAsync()));
        first.WaitForElement("gcds-input[name=Name]");
        await Input(first, "Name", "Not saved yet");
        await first.InvokeAsync(() => _context.Services.GetRequiredService<DraftFlushRegistry>().FlushAllAsync());
        Assert.True(_context.Drafts.Drafts.ContainsKey(Key(TestPrincipals.AdminId, "new")));
        await _context.DisposeComponentsAsync();

        var second = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.CreateAsync()));
        await Click(second, "[data-csla-draft] gcds-button[data-action=restore]");

        second.WaitForAssertion(() => Assert.Equal("Not saved yet", second.FindComponent<CslaForm<ProjectEdit>>().Instance.ViewModel.Model!.Name));
    }

    [Fact]
    public async Task Cancel_AfterAFailedSave_RestoresTheLoadedValues()
    {
        _context.Projects.DuplicateCodeOnSave = true;
        var host = Render<LiveFormHost>();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        await Input(host, "Name", "Mine");
        Assert.False(await host.InvokeAsync(form.SaveAsync));

        await host.InvokeAsync(form.CancelAsync);

        Assert.Equal("P-1", form.ViewModel.Model!.Name);
        Assert.False(form.IsDirty);
    }

    [Fact]
    public async Task OwnCommand_RefreshesTheForm()
    {
        var host = Render<LiveFormHost>();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        var origin = _context.Services.GetRequiredService<CircuitOrigin>().Id;
        _context.Projects.Projects[_projectId] = _context.Projects.Projects[_projectId] with { StatusCode = 4, Version = 6 };

        await _context.Hub.DispatchAsync(new EntityChange("Project", _projectId.ToString(), ChangeKind.Updated, 6, TestPrincipals.AdminId, "Me", origin));

        host.WaitForAssertion(() => Assert.Equal(4, form.ViewModel.Model!.StatusCode));
    }

    [Fact]
    public async Task Escape_WhenDirty_AsksBeforeDiscarding()
    {
        var host = Render<LiveFormHost>();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        await Input(host, "Name", "Mine");

        await host.InvokeAsync(form.ShortcutCancel);

        Assert.Equal("Mine", form.ViewModel.Model!.Name);
        await Click(host, "[data-csla-confirm] gcds-button[data-action=confirm]");
        host.WaitForAssertion(() => Assert.Equal("P-1", form.ViewModel.Model!.Name));
    }

    [Fact]
    public async Task Save_IsEnabledWhenDirtyEvenIfInvalid_AndShowsTheSummary()
    {
        var host = Render<ButtonFormHost>();
        await Input(host, "Name", string.Empty);

        host.WaitForAssertion(() => Assert.False(host.Find("gcds-button[data-kind=Save]").HasAttribute("disabled")));
        await Click(host, "gcds-button[data-kind=Save]");

        host.WaitForElement("gcds-error-summary");
    }

    [Fact]
    public async Task DisposedWhileLoading_LeavesNoPresence()
    {
        _context.Projects.CodeCheckGate = null;
        var gate = new TaskCompletionSource<ProjectEdit>();
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => gate.Task));

        await _context.DisposeComponentsAsync();
        gate.SetResult(await portal.FetchAsync(_projectId));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(_context.Presence.Present(ChangeTopic.ForRecord("Project", _projectId.ToString())));
    }

    // A draft written the way the form writes one: through an edit, at the form's edit level.
    private async Task StoreDraftThroughTheFormAsync(string description)
    {
        var host = Render<LiveFormHost>();
        await Input(host, "Description", description);
        await host.InvokeAsync(() => _context.Services.GetRequiredService<DraftFlushRegistry>().FlushAllAsync());
        await _context.DisposeComponentsAsync();
        Assert.True(_context.Drafts.Drafts.ContainsKey(Key(TestPrincipals.AdminId, _projectId.ToString())));
    }

    private void Use(System.Security.Claims.ClaimsPrincipal user)
    {
        _context.Dispose();
        _context = new ComponentTestContext(user);
        _context.Projects.Add(_projectId, "P-1", members: TestPrincipals.ClientId);
    }

    private static Task Input<T>(IRenderedComponent<T> host, string name, string value)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        Click(host, $"gcds-input[name={name}]", "ongcdsinput", value);

    private static Task Click<T>(IRenderedComponent<T> host, string selector, string eventName = "ongcdsclick", string? value = null)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        host.WaitForElement(selector);
        return host.InvokeAsync(() => host.Find(selector).TriggerEventAsync(eventName, new GcdsEventArgs { Value = value }));
    }

    private IRenderedComponent<T> Render<T>()
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<T>(builder =>
        {
            builder.OpenComponent<T>(0);
            builder.AddAttribute(1, "Load", (Func<Task<ProjectEdit>>)(() => portal.FetchAsync(_projectId)));
            builder.CloseComponent();
        });
        host.WaitForElement("gcds-input[name=Name]");
        return host;
    }
}
