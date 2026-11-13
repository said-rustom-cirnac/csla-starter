using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CslaStarter.Components.Tables;

/// <summary>
/// One row of a <c>CslaTable</c>: a cell per visible column. When rows can be selected, a row is a tab stop that a
/// click, Enter or Space selects; the selected row is <c>aria-current</c> (<c>aria-selected</c> belongs to grids).
/// </summary>
public partial class CslaTableRow<TItem>
    where TItem : class, CslaStarter.Core.IEntityIdentity
{
    [Parameter, EditorRequired]
    public TItem Item { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<CslaTableColumn<TItem>> Columns { get; set; } = [];

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public EventCallback<TItem> OnSelect { get; set; }

    private Task SelectAsync() => OnSelect.InvokeAsync(Item);

    private Task KeyDownAsync(KeyboardEventArgs e) => e.Key is "Enter" or " " ? SelectAsync() : Task.CompletedTask;
}
