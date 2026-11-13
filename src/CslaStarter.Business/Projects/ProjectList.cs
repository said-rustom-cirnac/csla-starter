using Csla;
using Csla.Rules;
using Csla.Serialization.Mobile;
using CslaStarter.Business.Base;
using CslaStarter.Business.Rules;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>
/// One page of the projects the user may see. Fetch with
/// <c>(string search, int statusCode, string sortBy, bool descending, int page, int pageSize)</c>; "" and 0 mean no filter,
/// <c>sortBy</c> is "Code", "Name" or "Status".
/// </summary>
[Serializable]
public sealed class ProjectList : ReadOnlyListBase<ProjectList, ProjectInfo>, IPagedList
{
    private int _totalCount;

    public int TotalCount => _totalCount;

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules() =>
        BusinessRules.AddRule(typeof(ProjectList), new IsSignedIn(AuthorizationActions.GetObject));

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
    private async Task Fetch(string search, int statusCode, string sortBy, bool descending, int page, int pageSize, [Inject] IProjectDal dal, [Inject] IChildDataPortal<ProjectInfo> rowPortal)
    {
        var result = await dal.FetchListAsync(
            new ProjectListCriteria(string.IsNullOrWhiteSpace(search) ? null : search, statusCode == 0 ? null : statusCode, sortBy, descending, page, pageSize),
            CancellationToken.None);
        _totalCount = result.TotalCount;
        using (LoadListMode)
        {
            foreach (var row in result.Items)
            {
                Add(rowPortal.FetchChild(row));
            }
        }
    }
}
