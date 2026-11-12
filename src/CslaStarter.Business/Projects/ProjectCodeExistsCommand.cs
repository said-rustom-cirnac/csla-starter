using Csla;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>Is a project code used by any project other than the given one? (Used by the UniqueProjectCode rule.)</summary>
[Serializable]
public sealed class ProjectCodeExistsCommand : CommandBase<ProjectCodeExistsCommand>
{
    public static readonly PropertyInfo<bool> ExistsProperty = RegisterProperty<bool>(nameof(Exists));

    public bool Exists
    {
        get => ReadProperty(ExistsProperty);
        private set => LoadProperty(ExistsProperty, value);
    }

    [Execute]
    private async Task Execute(string code, Guid excludingId, [Inject] IProjectDal dal) =>
        Exists = !await dal.IsCodeUniqueAsync(code, excludingId, CancellationToken.None);
}
