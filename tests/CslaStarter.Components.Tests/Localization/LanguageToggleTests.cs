using Bunit;
using Bunit.TestDoubles;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Localization;
using CslaStarter.Components.Tests.Forms;
using CslaStarter.Core.Drafts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Localization;

public sealed class LanguageToggleTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task LanguageToggle_FlushesDraftsBeforeNavigating()
    {
        var projectId = Guid.CreateVersion7();
        _context.Projects.Add(projectId, "P-1");
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/projects/1");
        var form = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(projectId)));
        form.WaitForElement("gcds-input[name=Name]");
        await form.InvokeAsync(() => form.Find("gcds-input[name=Name]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" }));
        var toggle = _context.Render<CslaLanguageToggle>();

        await toggle.InvokeAsync(() => toggle.Find("gcds-button").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        Assert.True(_context.Drafts.Drafts.ContainsKey(new DraftKey(TestPrincipals.AdminId, "Project", projectId.ToString())));
        var last = _context.Services.GetRequiredService<BunitNavigationManager>().History.First();
        Assert.Equal("/culture/set?culture=fr-CA&redirectUri=%2Fprojects%2F1", last.Uri);
        Assert.True(last.Options.ForceLoad);
    }
}
