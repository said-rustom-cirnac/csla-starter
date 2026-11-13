using Csla;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

[Serializable]
public sealed class ProjectTaskList : BusinessListBase<ProjectTaskList, ProjectTask>
{
    internal ProjectTask AddTask(string title, IChildDataPortal<ProjectTask> portal)
    {
        var task = portal.CreateChild(title, Count == 0 ? 1 : this.Max(t => t.SortOrder) + 1);
        Add(task);
        return task;
    }

    [FetchChild]
    private void FetchChild(IReadOnlyList<ProjectTaskDto> tasks, [Inject] IChildDataPortal<ProjectTask> portal)
    {
        using (LoadListMode)
        {
            foreach (var task in tasks)
            {
                Add(portal.FetchChild(task));
            }
        }
    }
}
