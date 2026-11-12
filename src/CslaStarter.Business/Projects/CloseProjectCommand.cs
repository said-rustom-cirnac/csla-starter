using Csla;
using CslaStarter.Business.Types;
using CslaStarter.Core.Resources;
using CslaStarter.Core.Security;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

public enum CloseProjectResult
{
    Closed = 1,
    HasOpenTasks = 2,
}

/// <summary>
/// Closes a project. Allowed for Admins, and for Technical staff who are members of the project (a per-record rule).
/// A project with open tasks is not closed; <see cref="Message"/> says why, in the user's language.
/// </summary>
[Serializable]
public sealed class CloseProjectCommand : CommandBase<CloseProjectCommand>
{
    public static readonly PropertyInfo<CloseProjectResult> ResultProperty = RegisterProperty<CloseProjectResult>(nameof(Result));

    public CloseProjectResult Result
    {
        get => ReadProperty(ResultProperty);
        private set => LoadProperty(ResultProperty, value);
    }

    public string Message => Result == CloseProjectResult.Closed ? Messages.Project_Closed : Messages.Project_CloseWithOpenTasks;

    [Execute]
    private async Task Execute(Guid projectId, [Inject] IProjectDal dal, [Inject] ICurrentUser user)
    {
        var project = await dal.FetchAsync(projectId, CancellationToken.None);
        var allowed = user.HasRole(UserRoleType.Admin)
            || (user.HasRole(UserRoleType.Technical) && project.Members.Any(m => m.UserId == user.UserId));
        if (!allowed)
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        if (project.Tasks.Any(t => !t.IsDone))
        {
            Result = CloseProjectResult.HasOpenTasks;
            return;
        }

        await dal.CloseAsync(projectId, ProjectStatusType.Closed, CancellationToken.None);
        Result = CloseProjectResult.Closed;
    }
}
