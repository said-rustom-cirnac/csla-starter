using System.Text.Json;
using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Types;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Notifications;
using CslaStarter.Components.Security;
using CslaStarter.Components.Services;
using CslaStarter.Components.Tests.Buttons;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Notifications;

public sealed class NotificationTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);
    private readonly Guid _projectId = Guid.CreateVersion7();

    public NotificationTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ErrorSummary_ListsErrorsWithFieldLinks_AndTakesFocus()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<ButtonFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_projectId)));
        host.WaitForElement("gcds-input[name=Name]");
        Assert.Empty(host.FindAll("gcds-error-summary"));

        await host.InvokeAsync(() => host.Find("gcds-input[name=Name]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = string.Empty }));
        await host.InvokeAsync(() => host.Find("form").SubmitAsync());

        var summary = host.WaitForElement("gcds-error-summary");
        using var links = JsonDocument.Parse(summary.GetAttribute("error-links")!);
        var nameId = host.Find("gcds-input[name=Name]").Id;
        Assert.True(links.RootElement.TryGetProperty("#" + nameId, out _), summary.GetAttribute("error-links"));
        _context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void ToastHost_ShowsToasts_AndDismisses()
    {
        var host = _context.Render<CslaToastHost>();
        var toasts = _context.Services.GetRequiredService<IToastService>();

        host.InvokeAsync(() => toasts.Show(ToastKind.Success, "Saved."));
        host.WaitForAssertion(() => Assert.Equal("success", host.Find("gcds-notice").GetAttribute("type")));
        host.InvokeAsync(() => host.Find("gcds-notice gcds-button").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        host.WaitForAssertion(() => Assert.Empty(host.FindAll("gcds-notice")));
    }

    [Fact]
    public void LiveRegion_SpeaksPoliteAndAssertiveMessages()
    {
        var host = _context.Render<CslaLiveRegion>();
        var announcer = _context.Services.GetRequiredService<ILiveAnnouncer>();

        announcer.Announce("Updated by Jane.");
        announcer.Announce("Something went wrong.", assertive: true);

        host.WaitForAssertion(() =>
        {
            Assert.Equal("Updated by Jane.", host.Find("[data-csla-live=polite]").TextContent);
            Assert.Equal("Something went wrong.", host.Find("[data-csla-live=assertive]").TextContent);
        });
    }

    [Fact]
    public void Authorize_ShowsContentOnlyToTheRoles()
    {
        var admin = _context.Render<CslaAuthorize>(p => p
            .Add(a => a.Roles, [UserRoleType.Admin])
            .Add(a => a.ChildContent, "<p>secret</p>")
            .Add(a => a.NotAuthorized, "<p>nope</p>"));
        var technical = _context.Render<CslaAuthorize>(p => p
            .Add(a => a.Roles, [UserRoleType.Technical])
            .Add(a => a.ChildContent, "<p>secret</p>")
            .Add(a => a.NotAuthorized, "<p>nope</p>"));

        Assert.Equal("secret", admin.Find("p").TextContent);
        Assert.Equal("nope", technical.Find("p").TextContent);
    }
}
