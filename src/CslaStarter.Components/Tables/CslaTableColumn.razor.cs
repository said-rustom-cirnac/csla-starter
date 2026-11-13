using CslaStarter.Components.Fields;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Tables;

/// <summary>
/// One column of a <c>CslaTable</c>. With <see cref="Property"/> the header is the property's friendly name, the
/// cell its value, and the column disappears for users who may not read it; <see cref="Template"/> overrides the cell.
/// It renders nothing itself: the table draws the header and cells.
/// </summary>
public partial class CslaTableColumn<TItem>
    where TItem : class, CslaStarter.Core.IEntityIdentity
{
    [Parameter]
    public string? Property { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public RenderFragment<TItem>? Template { get; set; }

    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>What the list's fetch sorts by; defaults to <see cref="Property"/>.</summary>
    [Parameter]
    public string? SortKey { get; set; }

    [CascadingParameter]
    private ITableColumns<TItem>? Table { get; set; }

    internal RenderFragment CellFor(TItem item) =>
        Template?.Invoke(item) ?? (builder => builder.AddContent(0, FieldValue.ToText(Property is null ? null : typeof(TItem).GetProperty(Property)?.GetValue(item))));

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SortKey ??= Property;
        (Table ?? throw new InvalidOperationException("CslaTableColumn must be inside a CslaTable.")).AddColumn(this);
    }
}
