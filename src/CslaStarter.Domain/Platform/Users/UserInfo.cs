using Csla;
using Csla.Rules;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Platform.Users;

/// <summary>One row of the user list.</summary>
[CslaImplementProperties]
public sealed partial class UserInfo : AppReadOnlyBase<UserInfo>
{
    public partial Guid Id { get; private set; }

    [Label("Display name")]
    public partial string DisplayName { get; private set; }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserInfo), new IsSignedIn(AuthorizationActions.GetObject));
    }

    [FetchChild]
    private void FetchChild(UserDto user)
    {
        Id = user.Id;
        DisplayName = user.DisplayName;
    }
}
