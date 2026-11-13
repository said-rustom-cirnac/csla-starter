using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Services;
using CslaStarter.Components.Session;
using CslaStarter.Components.Tests.Forms;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Session;

public sealed class SessionTimeoutTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);

    public void Dispose() => _context.Dispose();

    [Fact]
    public void SessionTimeout_Warning_OpensTheDialog_AndAnnounces()
    {
        var announced = new List<(string, bool)>();
        _context.Services.GetRequiredService<ILiveAnnouncer>().Announced += (m, a) => announced.Add((m, a));
        var timeout = _context.Render<CslaSessionTimeout>();

        _context.Session.Enter(SessionPhase.Warning);

        timeout.WaitForElement("[data-csla-session]");
        Assert.Contains(announced, a => a.Item2 && a.Item1.Contains('2', StringComparison.Ordinal));
        _context.SessionModule.VerifyInvoke("watchActivity");
    }

    [Fact]
    public async Task SessionTimeout_Stay_RecordsActivity_AndClosesTheDialog()
    {
        var timeout = _context.Render<CslaSessionTimeout>();
        _context.Session.Enter(SessionPhase.Warning);
        timeout.WaitForElement("[data-csla-session]");

        await timeout.InvokeAsync(() => timeout.Find("gcds-button[data-action=stay]").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        Assert.Equal(1, _context.Session.Activity);
        Assert.Empty(timeout.FindAll("[data-csla-session]"));
    }

    [Fact]
    public async Task SessionTimeout_Expired_FlushesDraftsThenSignsOut()
    {
        var projectId = Guid.CreateVersion7();
        _context.Projects.Add(projectId, "P-1");
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var form = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(projectId)));
        form.WaitForElement("gcds-input[name=Name]");
        await form.InvokeAsync(() => form.Find("gcds-input[name=Name]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" }));
        var timeout = _context.Render<CslaSessionTimeout>();

        _context.Session.Enter(SessionPhase.Expired);

        timeout.WaitForAssertion(() => _context.SessionModule.VerifyInvoke("submit"));
        Assert.True(_context.Drafts.Drafts.ContainsKey(new DraftKey(TestPrincipals.AdminId, "Project", projectId.ToString())));
        Assert.Equal("/account/signout", timeout.Find("form[data-csla-signout]").GetAttribute("action"));
    }
}
