using Bunit;
using Bunit.TestDoubles;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Forms;
using CslaStarter.Components.Interop;
using CslaStarter.Core.Drafts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Forms;

public sealed class ShortcutAndGuardTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);
    private readonly Guid _projectId = Guid.CreateVersion7();

    public ShortcutAndGuardTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Shortcuts_AttachToTheForm_CtrlSSaves_EscCancels()
    {
        var host = Render();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        _context.ShortcutsModule.VerifyInvoke("attach");

        await Input(host, "Saved by shortcut");
        await host.InvokeAsync(form.ShortcutSave);
        host.WaitForAssertion(() => Assert.Equal("Saved by shortcut", _context.Projects.LastSaved?.Name));

        await Input(host, "Escaped");
        await host.InvokeAsync(form.ShortcutCancel);
        host.WaitForAssertion(() => Assert.Equal("Saved by shortcut", host.Find("gcds-input[name=Name]").GetAttribute("value")));
    }

    [Fact]
    public async Task UnsavedChanges_InternalNavigation_AsksFirst_LeaveDiscards()
    {
        var host = Render();
        var navigation = _context.Services.GetRequiredService<BunitNavigationManager>();
        await Input(host, "Mine");
        _context.Time.Advance(TimeSpan.FromSeconds(3));
        var key = new DraftKey(TestPrincipals.AdminId, "Project", _projectId.ToString());
        host.WaitForAssertion(() => Assert.True(_context.Drafts.Drafts.ContainsKey(key)));

        await host.InvokeAsync(() => navigation.NavigateTo("/elsewhere"));

        Assert.DoesNotContain("/elsewhere", navigation.Uri, StringComparison.Ordinal);
        await host.InvokeAsync(() => host.Find("[data-csla-confirm] gcds-button[data-action=confirm]").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));
        host.WaitForAssertion(() => Assert.EndsWith("/elsewhere", navigation.Uri, StringComparison.Ordinal));
        Assert.False(_context.Drafts.Drafts.ContainsKey(key));
    }

    [Fact]
    public async Task UnsavedChanges_StayKeepsTheEdits()
    {
        var host = Render();
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        await Input(host, "Mine");

        await host.InvokeAsync(() => navigation.NavigateTo("/elsewhere"));
        await host.InvokeAsync(() => host.Find("[data-csla-confirm] gcds-button[data-action=cancel]").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        Assert.DoesNotContain("/elsewhere", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal("Mine", host.Find("gcds-input[name=Name]").GetAttribute("value"));
    }

    [Fact]
    public async Task CleanForm_NavigatesWithoutAsking()
    {
        Render();
        var navigation = _context.Services.GetRequiredService<NavigationManager>();

        await _context.Renderer.Dispatcher.InvokeAsync(() => navigation.NavigateTo("/elsewhere"));

        Assert.EndsWith("/elsewhere", navigation.Uri, StringComparison.Ordinal);
    }

    private static Task Input(IRenderedComponent<LiveFormHost> host, string value) =>
        host.InvokeAsync(() => host.Find("gcds-input[name=Name]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = value }));

    private IRenderedComponent<LiveFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_projectId)));
        host.WaitForElement("gcds-input[name=Name]");
        return host;
    }
}
