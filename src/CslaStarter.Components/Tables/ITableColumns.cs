namespace CslaStarter.Components.Tables;

/// <summary>How a column finds the table it belongs to (the table cascades itself under this interface).</summary>
public interface ITableColumns<TItem>
    where TItem : class, CslaStarter.Core.IEntityIdentity
{
    void AddColumn(CslaTableColumn<TItem> column);
}
