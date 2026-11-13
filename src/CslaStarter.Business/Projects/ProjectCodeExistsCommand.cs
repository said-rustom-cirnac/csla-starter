using Csla;
using Csla.Rules;
using CslaStarter.Business.Rules;
using CslaStarter.Business.Types;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>
/// Is a project code used by any project other than the given one? (Used by the UniqueProjectCode rule.) It looks past
/// record-level access, so only users who may edit projects can run it.
/// </summary>
[Serializable]
public sealed class ProjectCodeExistsCommand : CommandBase<ProjectCodeExistsCommand>
{
    public static readonly PropertyInfo<bool> ExistsProperty = RegisterProperty<bool>(nameof(Exists));

    public bool Exists
    {
        get => ReadProperty(ExistsProperty);
        private set => LoadProperty(ExistsProperty, value);
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules() =>
        BusinessRules.AddRule(typeof(ProjectCodeExistsCommand), new HasRole(AuthorizationActions.EditObject, UserRoleType.Admin, UserRoleType.Technical));

    [Execute]
    private async Task Execute(string code, Guid excludingId, [Inject] IProjectDal dal) =>
        Exists = !await dal.IsCodeUniqueAsync(code, excludingId, CancellationToken.None);
}
