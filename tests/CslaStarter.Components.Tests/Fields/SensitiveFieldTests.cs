using System.Security.Claims;
using Bunit;
using Csla;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Users;
using CslaStarter.Components.Interop;
using CslaStarter.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Fields;

public sealed class SensitiveFieldTests : IDisposable
{
    private readonly Guid _bob = Guid.CreateVersion7();
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public SensitiveFieldTests() => _context.Users.Add(_bob, "Bob", 2);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SensitiveField_RendersMask_RevealLogsEvent()
    {
        var host = Render();

        Assert.DoesNotContain("E-1", host.Markup, StringComparison.Ordinal);
        Assert.Equal("••••••", host.Find("gcds-input[name=EmployeeNumber]").GetAttribute("value"));
        Assert.Empty(_context.SecurityEvents.Written);

        await host.InvokeAsync(() => host.Find("gcds-button[data-action=reveal]").TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

        host.WaitForAssertion(() => Assert.Equal("E-1", host.Find("gcds-input[name=EmployeeNumber]").GetAttribute("value")));
        var written = Assert.Single(_context.SecurityEvents.Written);
        Assert.Equal((SecurityEventKind.SensitiveReveal, TestPrincipals.AdminId), (written.Kind, written.UserId));
        Assert.Contains("EmployeeNumber", written.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveField_UserWhoCannotRead_GetsNothing()
    {
        Use(TestPrincipals.Technical);

        var host = Render();

        Assert.Empty(host.FindAll("gcds-input[name=EmployeeNumber]"));
        Assert.Empty(host.FindAll("gcds-button[data-action=reveal]"));
        Assert.DoesNotContain("E-1", host.Markup, StringComparison.Ordinal);
    }

    private void Use(ClaimsPrincipal user)
    {
        _context.Dispose();
        _context = new ComponentTestContext(user);
        _context.Users.Add(_bob, "Bob", 2);
    }

    private IRenderedComponent<UserFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<UserEdit>>();
        var host = _context.Render<UserFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_bob)));
        host.WaitForElement("gcds-input[name=DisplayName]");
        return host;
    }
}
