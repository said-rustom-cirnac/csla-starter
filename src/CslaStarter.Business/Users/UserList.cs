using Csla;
using Csla.Rules;
using Csla.Serialization.Mobile;
using CslaStarter.Business.Rules;
using CslaStarter.Business.Types;
using CslaStarter.Dal.Users;

namespace CslaStarter.Business.Users;

/// <summary>
/// One page of users, filtered by name and role. Administrators and technical staff.
/// Fetch with <c>(string search, int roleCode, int page, int pageSize)</c>: an empty search and role code 0 mean "no filter".
/// </summary>
[Serializable]
public sealed class UserList : ReadOnlyListBase<UserList, UserInfo>
{
    private int _totalCount;

    /// <summary>Users matching the filter across all pages.</summary>
    public int TotalCount => _totalCount;

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserList), new HasRole(AuthorizationActions.GetObject, UserRoleType.Admin, UserRoleType.Technical));
    }

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
    private async Task Fetch(string search, int roleCode, int page, int pageSize, [Inject] IUserDal dal, [Inject] IChildDataPortal<UserInfo> rowPortal)
    {
        var result = await dal.FetchListAsync(
            new UserListCriteria(string.IsNullOrWhiteSpace(search) ? null : search, roleCode == 0 ? null : roleCode, page, pageSize),
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
