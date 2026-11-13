using Csla;
using CslaStarter.Core.Auditing;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Auditing;

/// <summary>One recorded insert, update or delete, with the fields it changed.</summary>
[Serializable]
public sealed class AuditHistoryEntry : ReadOnlyBase<AuditHistoryEntry>
{
    public static readonly PropertyInfo<long> IdProperty = RegisterProperty<long>(nameof(Id));
    public static readonly PropertyInfo<DateTimeOffset> OccurredAtProperty = RegisterProperty<DateTimeOffset>(nameof(OccurredAt));
    public static readonly PropertyInfo<string> ChangedByProperty = RegisterProperty<string>(nameof(ChangedBy));
    public static readonly PropertyInfo<string> ActionProperty = RegisterProperty<string>(nameof(Action));
    public static readonly PropertyInfo<string> RecordProperty = RegisterProperty<string>(nameof(Record));
    public static readonly PropertyInfo<AuditHistoryChangeList> ChangesProperty = RegisterProperty<AuditHistoryChangeList>(nameof(Changes));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public long Id => GetProperty(IdProperty);

    [LocalizedDisplayName(nameof(Labels.History_OccurredAt))]
    public DateTimeOffset OccurredAt => GetProperty(OccurredAtProperty);

    [LocalizedDisplayName(nameof(Labels.History_ChangedBy))]
    public string ChangedBy => GetProperty(ChangedByProperty)!;

    /// <summary>Insert, Update or Delete.</summary>
    [LocalizedDisplayName(nameof(Labels.History_Action))]
    public string Action => GetProperty(ActionProperty)!;

    /// <summary>The entity type of the changed row (the record itself, or one of its children).</summary>
    [LocalizedDisplayName(nameof(Labels.History_Record))]
    public string Record => GetProperty(RecordProperty)!;

    [LocalizedDisplayName(nameof(Labels.History_Changes))]
    public AuditHistoryChangeList Changes => GetProperty(ChangesProperty)!;

    [FetchChild]
    private void FetchChild(AuditEntry entry, IReadOnlyDictionary<string, IReadOnlyList<TypeDto>> typeRows, [Inject] IChildDataPortal<AuditHistoryChangeList> changesPortal)
    {
        LoadProperty(IdProperty, entry.Id);
        LoadProperty(OccurredAtProperty, entry.OccurredAt);
        LoadProperty(ChangedByProperty, entry.UserName);
        LoadProperty(ActionProperty, entry.Action);
        LoadProperty(RecordProperty, entry.EntityType);
        LoadProperty(ChangesProperty, changesPortal.FetchChild(entry.EntityType, entry.Changes, typeRows));
    }
}
