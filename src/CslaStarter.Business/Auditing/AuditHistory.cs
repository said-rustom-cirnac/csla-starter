using Csla;
using Csla.Rules;
using Csla.Serialization.Mobile;
using CslaStarter.Business.Base;
using CslaStarter.Business.Rules;
using CslaStarter.Core.Auditing;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Auditing;

/// <summary>
/// One page of a record's history (its own changes and its children's), newest first; optionally one field only.
/// Fetch with <c>(string entityType, string entityKey, string property, int page, int pageSize)</c>, "" for every field.
/// Record-level access applies: the history of a record the user can't see is not found.
/// </summary>
[Serializable]
public sealed class AuditHistory : ReadOnlyListBase<AuditHistory, AuditHistoryEntry>, IPagedList
{
    private int _totalCount;

    public int TotalCount => _totalCount;

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules() =>
        BusinessRules.AddRule(typeof(AuditHistory), new IsSignedIn(AuthorizationActions.GetObject));

    protected override void OnGetState(SerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        base.OnGetState(info);
        info.AddValue(nameof(_totalCount), _totalCount);
    }

    protected override void OnSetState(SerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        base.OnSetState(info);
        _totalCount = info.GetValue<int>(nameof(_totalCount));
    }

    [Fetch]
    private async Task Fetch(
        string entityType, string entityKey, string property, int page, int pageSize,
        [Inject] IAuditHistoryReader reader, [Inject] ITypeDal types, [Inject] IChildDataPortal<AuditHistoryEntry> entryPortal)
    {
        var result = await reader.GetAsync(
            new AuditQuery(entityType, entityKey, string.IsNullOrEmpty(property) ? null : property, Math.Max(page, 1), Math.Clamp(pageSize, 1, 200)),
            CancellationToken.None);
        var typeRows = new Dictionary<string, IReadOnlyList<TypeDto>>(StringComparer.Ordinal);
        foreach (var typeName in result.Entries.SelectMany(e => e.Changes).Select(c => c.TypeCodeOf).OfType<string>().Distinct())
        {
            typeRows[typeName] = await types.FetchAsync(typeName, CancellationToken.None);
        }

        _totalCount = result.TotalCount;
        using (LoadListMode)
        {
            foreach (var entry in result.Entries)
            {
                Add(entryPortal.FetchChild(entry, (IReadOnlyDictionary<string, IReadOnlyList<TypeDto>>)typeRows));
            }
        }
    }
}
