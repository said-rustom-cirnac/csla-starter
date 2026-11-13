using Csla;

namespace CslaStarter.Business.Users;

/// <summary>The roles a user holds. Assigning a role they already hold does nothing.</summary>
[Serializable]
public sealed class UserRoleAssignmentList : BusinessListBase<UserRoleAssignmentList, UserRoleAssignment>
{
    public IEnumerable<int> RoleCodes => this.Select(r => r.RoleTypeCode);

    public bool Holds(int roleTypeCode) => this.Any(r => r.RoleTypeCode == roleTypeCode);

    internal void Assign(int roleTypeCode, IChildDataPortal<UserRoleAssignment> portal)
    {
        if (!Holds(roleTypeCode))
        {
            Add(portal.CreateChild(roleTypeCode));
        }
    }

    internal void Unassign(int roleTypeCode)
    {
        foreach (var assignment in this.Where(r => r.RoleTypeCode == roleTypeCode).ToList())
        {
            Remove(assignment);
        }
    }

    [FetchChild]
    private void FetchChild(IReadOnlyList<int> roleCodes, [Inject] IChildDataPortal<UserRoleAssignment> portal)
    {
        using (LoadListMode)
        {
            foreach (var code in roleCodes.Order())
            {
                Add(portal.FetchChild(code));
            }
        }
    }
}
