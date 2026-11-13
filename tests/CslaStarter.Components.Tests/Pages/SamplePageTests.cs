using Bunit;
using Bunit.TestDoubles;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Services;
using CslaStarter.Core.Resources;
using CslaStarter.Web.Components.Pages.Sample.Projects;
using CslaStarter.Web.Components.Pages.Sample.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Pages;

public sealed class SamplePageTests : IDisposable
{
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ProjectList_ShowsProjects_AndNewForCreators()
    {
        _context.Projects.Add(Guid.CreateVersion7(), "P-1");

        var page = _context.Render<ProjectListPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Single(page.FindAll("gcds-button[data-kind=New]"));
    }

    [Fact]
    public void ProjectList_Client_HasNoNewButton()
    {
        Use(TestPrincipals.Client);
        _context.Projects.Add(Guid.CreateVersion7(), "P-1", members: TestPrincipals.ClientId);

        var page = _context.Render<ProjectListPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Empty(page.FindAll("gcds-button[data-kind=New]"));
        Assert.Empty(page.FindAll("th[data-property=Budget]"));
    }

    [Fact]
    public async Task ProjectEdit_New_SavesAndGoesToTheProject()
    {
        var page = _context.Render<ProjectEditPage>();
        page.WaitForElement("gcds-input[name=Code]");

        await page.InvokeAsync(() => page.Find("gcds-input[name=Code]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "P-NEW" }));
        await page.InvokeAsync(() => page.Find("gcds-input[name=Name]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "New project" }));
        page.WaitForAssertion(() => Assert.False(page.Find("gcds-button[data-kind=Save]").HasAttribute("disabled")));
        await page.InvokeAsync(() => page.Find("gcds-button[data-kind=Save]").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        page.WaitForAssertion(() => Assert.EndsWith($"projects/{_context.Projects.LastSaved!.Id}", _context.Services.GetRequiredService<BunitNavigationManager>().Uri, StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectEdit_HiddenProject_IsNotFound()
    {
        var page = _context.Render<ProjectEditPage>(p => p.Add(e => e.Id, Guid.CreateVersion7()));

        page.WaitForAssertion(() => Assert.Contains(_context.Services.GetRequiredService<IToastService>().Toasts, t => t.Message == Messages.Error_NotFound));
        Assert.Empty(page.FindAll("gcds-input"));
    }

    [Fact]
    public void UserList_Client_IsNotAuthorized()
    {
        Use(TestPrincipals.Client);

        var page = _context.Render<UserListPage>();

        Assert.Contains(Messages.AccessDenied_Body, page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("table"));
    }

    private void Use(System.Security.Claims.ClaimsPrincipal user)
    {
        _context.Dispose();
        _context = new ComponentTestContext(user);
    }
}
