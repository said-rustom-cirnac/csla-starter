using Csla;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

[Serializable]
public sealed class ProjectMemberList : BusinessListBase<ProjectMemberList, ProjectMember>
{
    public bool Contains(Guid userId) => this.Any(m => m.UserId == userId);

    internal void AddMember(Guid userId, string displayName, IChildDataPortal<ProjectMember> portal)
    {
        if (!Contains(userId))
        {
            Add(portal.CreateChild(userId, displayName));
        }
    }

    internal void RemoveMember(Guid userId)
    {
        foreach (var member in this.Where(m => m.UserId == userId).ToList())
        {
            Remove(member);
        }
    }

    [FetchChild]
    private void FetchChild(IReadOnlyList<ProjectMemberDto> members, [Inject] IChildDataPortal<ProjectMember> portal)
    {
        using (LoadListMode)
        {
            foreach (var member in members)
            {
                Add(portal.FetchChild(member));
            }
        }
    }
}
