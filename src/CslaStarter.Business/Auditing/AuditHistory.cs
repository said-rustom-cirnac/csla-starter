using Csla;
using Csla.Core.FieldManager;
using Csla.Rules;
using Csla.Security;
using Csla.Serialization.Mobile;
using CslaStarter.Business.Base;
using CslaStarter.Business.Rules;
using CslaStarter.Core.Auditing;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Auditing;

/// <summary>
/// One page of a record's history (its own changes and its children's), newest first; optionally one field only.
/// Fetch with <c>(string entityType, string entityKey, string property, int page, int pageSize)</c>, "" for every field.
/// The history shows what the record shows: the type marked <see cref="HistoryOfAttribute"/> for the entity type must
/// let the user open it, fields they may not read are left out, and record-level access applies (the history of a
/// record the user can't see is not found).
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
        var guard = AuditedTypes.Find(entityType);
        if (guard is null || !BusinessRules.HasPermission(ApplicationContext, AuthorizationActions.GetObject, guard))
        {
            throw new Csla.Security.SecurityException(Messages.Error_AccessDenied);
        }

        var hidden = HiddenFields();
        if (!string.IsNullOrEmpty(property) && hidden.Contains($"{entityType}.{property}"))
        {
            return;
        }

        var result = await reader.GetAsync(
            new AuditQuery(entityType, entityKey, string.IsNullOrEmpty(property) ? null : property, Math.Max(page, 1), Math.Clamp(pageSize, 1, 200), hidden),
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

    // "EntityType.Property" for every audited field the user may not read, asked of an empty instance of the type that
    // guards it (read rules are role rules, not state rules).
    private HashSet<string> HiddenFields()
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityType in AuditedTypes.EntityTypes)
        {
            var type = AuditedTypes.Find(entityType)!;
            var probe = (IAuthorizeReadWrite)ApplicationContext.CreateInstanceDI(type);
            foreach (var registered in PropertyInfoManager.GetRegisteredProperties(type))
            {
                if (!probe.CanReadProperty(registered.Name))
                {
                    hidden.Add($"{entityType}.{registered.Name}");
                }
            }
        }

        return hidden;
    }
}
