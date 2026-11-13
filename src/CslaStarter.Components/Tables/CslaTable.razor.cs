using Csla.Security;
using CslaStarter.Business.Base;
using CslaStarter.Components.Interop;
using CslaStarter.Core.Realtime;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Tables;

/// <summary>
/// A read-only, server-paged list in a semantic table. <see cref="Load"/> fetches one page for the current
/// <see cref="TableState"/> (record-level access applies, so totals are right). When any record of
/// <see cref="EntityType"/> changes, the table refreshes once, 500 ms after the last notice, keeping page, sort and
/// selection. Columns the user may not read are left out.
/// </summary>
public partial class CslaTable<TList, TItem> : ITableColumns<TItem>
    where TList : class, IEnumerable<TItem>
    where TItem : class, CslaStarter.Core.IEntityIdentity
{
    private static readonly TimeSpan RefreshAfter = TimeSpan.FromMilliseconds(500);

    private readonly List<CslaTableColumn<TItem>> _columns = [];
    private ITimer? _debounce;

    /// <summary>The type topic to follow (any record of this type changing refreshes the table).</summary>
    [Parameter, EditorRequired]
    public string EntityType { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public Func<TableState, Task<TList>> Load { get; set; } = default!;

    /// <summary>The columns (<c>CslaTableColumn</c>).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public EventCallback<TItem> RowSelected { get; set; }

    [Parameter]
    public string? InitialSort { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 25;

    public TableState State { get; } = new();

    protected IReadOnlyList<TItem> Items { get; private set; } = [];

    protected int TotalCount { get; private set; }

    protected int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)State.PageSize));

    protected IReadOnlyList<CslaTableColumn<TItem>> VisibleColumns =>
        [.. _columns.Where(column => column.Property is null || Items.Count == 0 || Items[0] is not IAuthorizeReadWrite row || row.CanReadProperty(column.Property))];

    protected override string? TelemetryEntityType => EntityType;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Fetches the current page again.</summary>
    public Task RefreshAsync() => RunAsync("refresh", async _ =>
    {
        var list = await Load(State);
        Items = [.. list];
        TotalCount = (list as IPagedList)?.TotalCount ?? Items.Count;
    });

    public void AddColumn(CslaTableColumn<TItem> column)
    {
        _columns.Add(column);
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        State.SortBy ??= InitialSort;
        State.PageSize = PageSize;
        _debounce = Time.CreateTimer(_ => _ = InvokeAsync(RefreshAsync), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Subscribe(ChangeTopic.ForType(EntityType), _ => ScheduleRefresh(), includeOwn: true);
        OnResync(RefreshAsync);
        await RefreshAsync();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _debounce?.Dispose();
        return base.DisposeAsyncCore();
    }

    protected string HeaderText(CslaTableColumn<TItem> column) =>
        column.Title
        ?? Csla.Core.FieldManager.PropertyInfoManager.GetRegisteredProperties(typeof(TItem)).FirstOrDefault(p => p.Name == column.Property)?.FriendlyName
        ?? column.Property
        ?? string.Empty;

    protected string? AriaSort(CslaTableColumn<TItem> column) =>
        !column.Sortable ? null
        : column.SortKey != State.SortBy ? "none"
        : State.Descending ? "descending" : "ascending";

    protected async Task SortAsync(CslaTableColumn<TItem> column)
    {
        if (State.SortBy == column.SortKey)
        {
            State.Descending = !State.Descending;
        }
        else
        {
            State.SortBy = column.SortKey;
            State.Descending = false;
        }

        State.Page = 1;
        await RefreshAsync();
    }

    protected async Task OnPageAsync(GcdsEventArgs e)
    {
        if (int.TryParse(e.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var page) && page >= 1 && page <= TotalPages)
        {
            State.Page = page;
            await RefreshAsync();
        }
    }

    protected async Task SelectAsync(TItem item)
    {
        State.SelectedKey = item.EntityKey;
        await RowSelected.InvokeAsync(item);
    }

    private Task ScheduleRefresh()
    {
        _debounce?.Change(RefreshAfter, Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }
}
