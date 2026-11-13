using CslaStarter.Business.Types;

namespace CslaStarter.Components.Types;

/// <summary>Dropdown, radio or checkbox options from a *Type list: active values plus the record's current one, in the current language.</summary>
public static class TypeOptions
{
    public static IReadOnlyList<KeyValuePair<int, string>> From<TList, TItem>(CslaTypeList<TList, TItem> list, int? current)
        where TList : CslaTypeList<TList, TItem>
        where TItem : CslaType<TItem>
    {
        ArgumentNullException.ThrowIfNull(list);
        return [.. list.ActiveOrCurrent(current).Select(item => new KeyValuePair<int, string>(item.Code, item.Description))];
    }
}
