using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Tables;

/// <summary>One row of a <c>CslaTable</c>: a cell per visible column; clicking selects it.</summary>
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
}
