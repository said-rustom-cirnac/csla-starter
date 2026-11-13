using CslaStarter.Business.Auditing;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;
using IDataPortalOfHistory = Csla.IDataPortal<CslaStarter.Business.Auditing.AuditHistory>;

namespace CslaStarter.Components.History;

/// <summary>
/// A record's change history, newest first (its own changes and its children's), or one field's with
/// <see cref="Property"/>. Masked values show as "(hidden)". The history of a record the user can't see is not found.
/// </summary>
public partial class CslaHistory
{
    private readonly List<AuditHistoryEntry> _entries = [];
    private int _page;

    [Parameter, EditorRequired]
    public string EntityType { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string EntityKey { get; set; } = string.Empty;

    [Parameter]
    public string? Property { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 25;

    protected IReadOnlyList<AuditHistoryEntry> Entries => _entries;

    protected int TotalCount { get; private set; }

    protected override string? TelemetryEntityType => EntityType;

    [Inject]
    private IDataPortalOfHistory Portal { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        _entries.Clear();
        _page = 0;
        await LoadMoreAsync();
    }

    protected Task LoadMoreAsync() => RunAsync("load", async _ =>
    {
        var page = await Portal.FetchAsync(EntityType, EntityKey, Property ?? string.Empty, _page + 1, PageSize);
        _page++;
        _entries.AddRange(page);
        TotalCount = page.TotalCount;
    });

    private static string ActionText(string action) => action switch
    {
        "Insert" => Messages.History_Inserted,
        "Delete" => Messages.History_Deleted,
        _ => Messages.History_Updated,
    };

    private static string ChangeText(AuditHistoryChange change) =>
        change.IsMasked ? Messages.History_Masked : $"{change.OldValue ?? "—"} → {change.NewValue ?? "—"}";
}
