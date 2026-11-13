using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Tables;
using CslaStarter.Core.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Tables;

public sealed class TableTests : IDisposable
{
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public TableTests() => Seed();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Table_LoadsFirstPage_RendersASemanticTableAndPagination()
    {
        var host = Render();

        Assert.Equal(3, host.FindAll("tbody tr").Count);
        Assert.Equal(["col", "col", "col"], host.FindAll("thead th").Select(th => th.GetAttribute("scope")));
        Assert.Equal("ascending", host.Find("th[data-property=Code]").GetAttribute("aria-sort"));
        var pagination = host.Find("gcds-pagination");
        Assert.Equal(("1", "1"), (pagination.GetAttribute("total-pages"), pagination.GetAttribute("current-page")));
        Assert.Equal(("Code", false, 1, 25), (_context.Projects.LastListCriteria!.SortBy, _context.Projects.LastListCriteria.Descending, _context.Projects.LastListCriteria.Page, _context.Projects.LastListCriteria.PageSize));
    }

    [Fact]
    public async Task Table_Sort_ReloadsWithTheNewState()
    {
        var host = Render();

        await ClickAsync(host, "th[data-property=Code] button");
        host.WaitForAssertion(() => Assert.True(_context.Projects.LastListCriteria!.Descending));
        await ClickAsync(host, "th[data-property=Name] button");

        host.WaitForAssertion(() => Assert.Equal(("Name", false), (_context.Projects.LastListCriteria!.SortBy, _context.Projects.LastListCriteria.Descending)));
        Assert.Equal("ascending", host.Find("th[data-property=Name]").GetAttribute("aria-sort"));
        Assert.Equal("none", host.Find("th[data-property=Code]").GetAttribute("aria-sort"));
    }

    [Fact]
    public async Task Table_TypeNotice_RefreshesOnceAfter500ms_KeepingSortAndSelection()
    {
        var host = Render();
        await ClickAsync(host, "th[data-property=Code] button");
        host.WaitForAssertion(() => Assert.True(_context.Projects.LastListCriteria!.Descending));
        await ClickAsync(host, "tbody tr");
        var selected = host.Instance.Selected!.EntityKey;
        var fetches = _context.Projects.ListFetches;

        for (var i = 0; i < 3; i++)
        {
            await _context.Hub.DispatchAsync(new EntityChange("Project", Guid.CreateVersion7().ToString(), ChangeKind.Inserted, 1, null, "Jane", "other"));
        }

        _context.Time.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Equal(fetches, _context.Projects.ListFetches);
        _context.Time.Advance(TimeSpan.FromMilliseconds(200));

        host.WaitForAssertion(() => Assert.Equal(fetches + 1, _context.Projects.ListFetches));
        Assert.True(_context.Projects.LastListCriteria!.Descending);
        Assert.Equal("true", host.Find($"tbody tr[data-key='{selected}']").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Table_Resync_Refreshes()
    {
        var host = Render();
        var fetches = _context.Projects.ListFetches;

        await _context.Hub.RaiseResyncAsync();

        host.WaitForAssertion(() => Assert.Equal(fetches + 1, _context.Projects.ListFetches));
    }

    [Fact]
    public async Task Table_Pagination_LoadsTheChosenPage()
    {
        for (var i = 0; i < 27; i++)
        {
            _context.Projects.Add(Guid.CreateVersion7(), $"P-{i:00}");
        }

        var host = Render();
        Assert.Equal("2", host.Find("gcds-pagination").GetAttribute("total-pages"));

        await host.InvokeAsync(() => host.Find("gcds-pagination").TriggerEventAsync("ongcdsclick", new GcdsEventArgs { Value = "2" }));

        host.WaitForAssertion(() => Assert.Equal(2, _context.Projects.LastListCriteria!.Page));
    }

    [Fact]
    public void Table_UnreadableColumn_IsHidden()
    {
        _context.Dispose();
        _context = new ComponentTestContext(TestPrincipals.Client);
        Seed();

        var host = Render();

        Assert.Empty(host.FindAll("th[data-property=Budget]"));
        Assert.Equal(2, host.FindAll("tbody tr:first-child td").Count);
    }

    // Find and click in one renderer turn, so a re-render can't swap the element in between.
    private static Task ClickAsync(IRenderedComponent<ProjectTableHost> host, string selector) =>
        host.InvokeAsync(() => host.Find(selector).ClickAsync(new()));

    private void Seed()
    {
        _context.Projects.Add(Guid.CreateVersion7(), "P-A", members: TestPrincipals.ClientId);
        _context.Projects.Add(Guid.CreateVersion7(), "P-B", members: TestPrincipals.ClientId);
        _context.Projects.Add(Guid.CreateVersion7(), "P-C", members: TestPrincipals.ClientId);
    }

    private IRenderedComponent<ProjectTableHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectList>>();
        var host = _context.Render<ProjectTableHost>(p => p.Add(h => h.Load, state => portal.FetchAsync(state.Filter, 0, state.SortBy ?? "Code", state.Descending, state.Page, state.PageSize)));
        host.WaitForElement("tbody tr");
        return host;
    }
}
