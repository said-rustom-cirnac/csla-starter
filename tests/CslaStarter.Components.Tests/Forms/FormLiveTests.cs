using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Forms;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Services;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Forms;

public sealed class FormLiveTests : IDisposable
{
    private static readonly Guid Jane = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public FormLiveTests() => _context.Projects.Add(_projectId, "P-1", members: TestPrincipals.ClientId);

    public void Dispose() => _context.Dispose();

    private ChangeTopic Topic => ChangeTopic.ForRecord("Project", _projectId.ToString());

    [Fact]
    public async Task Form_CleanEditor_RefetchesAndAnnounces()
    {
        var host = Render();
        var announced = new List<string>();
        _context.Services.GetRequiredService<ILiveAnnouncer>().Announced += (message, _) => announced.Add(message);

        await TheySaveAsync(p => p with { Name = "Theirs" });

        host.WaitForAssertion(() =>
        {
            Assert.Equal("Theirs", Field(host, "Name").GetAttribute("value"));
            Assert.NotNull(Field(host, "Name").ParentElement!.GetAttribute("data-highlighted"));
        });
        Assert.Null(Field(host, "Description").ParentElement!.GetAttribute("data-highlighted"));
        Assert.Contains(Messages.Live_UpdatedBy.Replace("{0}", "Jane", StringComparison.Ordinal), announced);
    }

    [Fact]
    public async Task Form_DirtyEditor_ShowsBannerAndKeepsEdits()
    {
        var host = Render();
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });

        await TheySaveAsync(p => p with { Name = "Theirs" });

        host.WaitForElement("[data-csla-banner]");
        Assert.Equal("Mine", Field(host, "Name").GetAttribute("value"));
        await host.Find("[data-csla-banner] gcds-button[data-action=reload]").TriggerEventAsync("ongcdsclick", EventArgs.Empty);
        host.WaitForAssertion(() => Assert.Equal("Theirs", Field(host, "Name").GetAttribute("value")));
        Assert.Empty(host.FindAll("[data-csla-banner]"));
    }

    [Fact]
    public async Task Form_DirtyEditor_KeepMine_HidesTheBanner()
    {
        var host = Render();
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });
        await TheySaveAsync(p => p with { Name = "Theirs" });
        host.WaitForElement("[data-csla-banner]");

        await host.Find("[data-csla-banner] gcds-button[data-action=keep]").TriggerEventAsync("ongcdsclick", EventArgs.Empty);

        Assert.Empty(host.FindAll("[data-csla-banner]"));
        Assert.Equal("Mine", Field(host, "Name").GetAttribute("value"));
    }

    [Fact]
    public async Task Form_OwnSave_IsIgnored()
    {
        var host = Render();
        var fetches = _context.Projects.Fetches;
        var origin = _context.Services.GetRequiredService<CircuitOrigin>().Id;

        await _context.Hub.DispatchAsync(new EntityChange("Project", _projectId.ToString(), ChangeKind.Updated, 6, TestPrincipals.AdminId, "Me", origin));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(fetches, _context.Projects.Fetches);
    }

    [Fact]
    public async Task Form_Resync_RefetchesWhenVersionChanged()
    {
        var host = Render();
        _context.Projects.Projects[_projectId] = _context.Projects.Projects[_projectId] with { Name = "Missed", Version = 6 };

        await _context.Hub.RaiseResyncAsync();

        host.WaitForAssertion(() => Assert.Equal("Missed", Field(host, "Name").GetAttribute("value")));
    }

    [Fact]
    public async Task Form_AccessLost_ClosesAndDiscardsDraft()
    {
        _context.Dispose();
        _context = new ComponentTestContext(TestPrincipals.Client);
        _context.Projects.Add(_projectId, "P-1", members: TestPrincipals.ClientId);
        var key = new DraftKey(TestPrincipals.ClientId, "Project", _projectId.ToString());
        _context.Drafts.Drafts[key] = new Draft(5, [1], DateTimeOffset.UnixEpoch);
        var host = Render();

        _context.Projects.Projects.Remove(_projectId);
        await _context.Hub.DispatchAsync(EntityChange.UserAccess(TestPrincipals.ClientId, TestPrincipals.AdminId, "Ada Admin", "other-circuit"));

        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.ClosedCount));
        Assert.False(_context.Drafts.Drafts.ContainsKey(key));
        Assert.Contains(_context.Services.GetRequiredService<IToastService>().Toasts, t => t.Kind == ToastKind.Warning && t.Message == Messages.Live_AccessLost);
        Assert.Empty(host.FindAll("gcds-input"));
    }

    [Fact]
    public async Task Form_ConcurrencyOnSave_OpensConflictDialog_ApplyMineMerges()
    {
        var host = Render();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });
        _context.Projects.Projects[_projectId] = _context.Projects.Projects[_projectId] with { Description = "Their description", Version = 6 };

        Assert.False(await host.InvokeAsync(form.SaveAsync));

        host.WaitForElement("[data-csla-conflict]");
        Assert.Contains("Mine", host.Find("[data-csla-conflict]").TextContent, StringComparison.Ordinal);
        await host.Find("[data-csla-conflict] gcds-button[data-action=apply-mine]").TriggerEventAsync("ongcdsclick", EventArgs.Empty);
        host.WaitForAssertion(() => Assert.Empty(host.FindAll("[data-csla-conflict]")));
        Assert.Equal(("Mine", "Their description", 6u), (form.ViewModel.Model!.Name, form.ViewModel.Model.Description, form.ViewModel.Model.Version));
        Assert.True(form.ViewModel.IsDirty);
        Assert.True(await host.InvokeAsync(form.SaveAsync));
        Assert.Equal(("Mine", "Their description"), (_context.Projects.LastSaved!.Name, _context.Projects.LastSaved.Description));
    }

    [Fact]
    public async Task Form_Presence_JoinsAndSwitchesToEditing()
    {
        var host = Render();
        Assert.Equal(PresenceMode.Viewing, Assert.Single(_context.Presence.Present(Topic)).Mode);

        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });

        host.WaitForAssertion(() => Assert.Equal(PresenceMode.Editing, Assert.Single(_context.Presence.Present(Topic)).Mode));
    }

    [Fact]
    public void Form_Presence_ShowsOthers()
    {
        var host = Render();

        using var jane = _context.Presence.Join(Topic, new PresenceEntry(Jane, "Jane", "jane-circuit", PresenceMode.Editing));

        host.WaitForAssertion(() => Assert.Contains("Jane", host.Find("[data-csla-presence]").TextContent, StringComparison.Ordinal));
    }

    private async Task TheySaveAsync(Func<ProjectDto, ProjectDto> change)
    {
        var saved = change(_context.Projects.Projects[_projectId]) with { Version = _context.Projects.Projects[_projectId].Version + 1 };
        _context.Projects.Projects[_projectId] = saved;
        await _context.Hub.DispatchAsync(new EntityChange("Project", _projectId.ToString(), ChangeKind.Updated, saved.Version, Jane, "Jane", "jane-circuit"));
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<LiveFormHost> host, string name) => host.Find($"gcds-input[name={name}]");

    private IRenderedComponent<LiveFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_projectId)));
        host.WaitForElement("gcds-input[name=Name]");
        return host;
    }
}
