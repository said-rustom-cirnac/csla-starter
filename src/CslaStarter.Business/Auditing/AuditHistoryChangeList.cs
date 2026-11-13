using Csla;
using CslaStarter.Core.Auditing;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Auditing;

[Serializable]
public sealed class AuditHistoryChangeList : ReadOnlyListBase<AuditHistoryChangeList, AuditHistoryChange>
{
    [FetchChild]
    private void FetchChild(string entityType, IReadOnlyList<AuditChange> changes, IReadOnlyDictionary<string, IReadOnlyList<TypeDto>> typeRows, [Inject] IChildDataPortal<AuditHistoryChange> portal)
    {
        using (LoadListMode)
        {
            foreach (var change in changes)
            {
                Add(portal.FetchChild(entityType, change, typeRows));
            }
        }
    }
}
