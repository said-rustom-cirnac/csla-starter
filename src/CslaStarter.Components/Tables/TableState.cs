namespace CslaStarter.Components.Tables;

/// <summary>What a table shows: page, sort, filter and the selected row. It survives live refreshes.</summary>
public sealed class TableState
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    /// <summary>A column's sort key ("Code", "Name"...), passed to the list's fetch.</summary>
    public string? SortBy { get; set; }

    public bool Descending { get; set; }

    public string Filter { get; set; } = string.Empty;

    /// <summary>The selected row's entity key.</summary>
    public string? SelectedKey { get; set; }
}
