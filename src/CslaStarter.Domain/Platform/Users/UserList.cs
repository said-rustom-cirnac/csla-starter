using Csla;
using Csla.Rules;
using CslaStarter.Domain.Platform.Rules;

namespace CslaStarter.Domain.Platform.Users;

/// <summary>Every user, in the order the DAL returns them.</summary>
public sealed partial class UserList : AppReadOnlyListBase<UserList, UserInfo>
{
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserList), new IsSignedIn(AuthorizationActions.GetObject));
    }

    [Fetch]
    private async Task Fetch([Inject] IUserDal dal, [Inject] IChildDataPortal<UserInfo> portal)
    {
        var users = await dal.ListAsync(CancellationToken.None);
        using (LoadListMode)
        {
            foreach (var user in users)
            {
                Add(portal.FetchChild(user));
            }
        }
    }
}
