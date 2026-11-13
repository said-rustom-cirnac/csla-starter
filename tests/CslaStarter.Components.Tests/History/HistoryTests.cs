using Bunit;
using CslaStarter.Business.Tests;
using CslaStarter.Components.History;
using CslaStarter.Components.Services;
using CslaStarter.Core.Auditing;
using CslaStarter.Core.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.History;

public sealed class HistoryTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);

    public void Dispose() => _context.Dispose();

    [Fact]
    public void History_RendersEntries_MaskedValuesHidden()
    {
        _context.AuditReader.Entries.Add(new AuditEntry(2, DateTimeOffset.UnixEpoch, null, "Jane", "Update", "User", "u-1",
            [new AuditChange("DisplayName", "Bob", "Bob B.", false, null), new AuditChange("EmployeeNumber", null, null, true, null)]));

        var history = _context.Render<CslaHistory>(p => p.Add(h => h.EntityType, "User").Add(h => h.EntityKey, "u-1"));

        history.WaitForAssertion(() => Assert.Single(history.FindAll("li")));
        var text = history.Find("li").TextContent;
        Assert.Contains("Jane", text, StringComparison.Ordinal);
        Assert.Contains("Bob → Bob B.", text, StringComparison.Ordinal);
        Assert.Contains(Messages.History_Masked, text, StringComparison.Ordinal);
    }

    [Fact]
    public void History_HiddenRecord_ShowsAnErrorNotTheHistory()
    {
        _context.AuditReader.Hidden.Add("p-hidden");

        var history = _context.Render<CslaHistory>(p => p.Add(h => h.EntityType, "Project").Add(h => h.EntityKey, "p-hidden"));

        var toasts = _context.Services.GetRequiredService<IToastService>();
        history.WaitForAssertion(() => Assert.Contains(toasts.Toasts, t => t.Message == Messages.Error_NotFound));
        Assert.Empty(history.FindAll("li"));
    }
}
