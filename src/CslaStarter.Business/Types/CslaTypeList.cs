using Csla;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Types;

/// <summary>
/// All values of one *Type lookup, in sort order. Fetch it in the scope that uses it (a CSLA object keeps its scope's
/// ApplicationContext, so never share one across users); the type DAL caches the rows, so fetching is cheap.
/// </summary>
[Serializable]
public abstract class CslaTypeList<TList, TItem> : ReadOnlyListBase<TList, TItem>
    where TList : CslaTypeList<TList, TItem>
    where TItem : CslaType<TItem>
{
    public TItem? Find(int code) => this.FirstOrDefault(item => item.Code == code);

    /// <summary>Values to offer in a dropdown: active ones, plus the record's current value even if it was retired.</summary>
    public IEnumerable<TItem> ActiveOrCurrent(int? currentCode) =>
        this.Where(item => item.IsActive || item.Code == currentCode);

    [Fetch]
    protected async Task Fetch([Inject] ITypeDal dal, [Inject] IChildDataPortal<TItem> childPortal)
    {
        ArgumentNullException.ThrowIfNull(dal);
        ArgumentNullException.ThrowIfNull(childPortal);
        var rows = await dal.FetchAsync(typeof(TItem).Name, CancellationToken.None);
        using (LoadListMode)
        {
            foreach (var row in rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Code))
            {
                Add(await childPortal.FetchChildAsync(row));
            }
        }
    }
}
