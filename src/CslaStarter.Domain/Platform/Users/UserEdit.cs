using Csla;
using Csla.Rules;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Platform.Users;

/// <summary>A user's details, as an administrator edits them. Accounts come from sign-in: there is no create or delete.</summary>
[CslaImplementProperties]
public sealed partial class UserEdit : AppRootBusinessBase<UserEdit>
{
    public partial Guid Id { get; private set; }

    [Label("Display name"), RequiredField, MaxLengthOf(200)]
    public partial string DisplayName { get; set; }

    protected override void AddBusinessRules()
    {
        // The base turns the rule attributes on the properties into rules.
        base.AddBusinessRules();
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserEdit), new IsSignedIn(AuthorizationActions.GetObject));
        BusinessRules.AddRule(typeof(UserEdit), new IsSignedIn(AuthorizationActions.EditObject));
    }

    [Fetch]
    private async Task Fetch(Guid id, [Inject] IUserDal dal)
    {
        var user = await dal.GetAsync(id, CancellationToken.None);
        using (BypassPropertyChecks)
        {
            Id = user.Id;
            DisplayName = user.DisplayName;
        }

        LoadVersion(user.Version);
    }

    [Update]
    private async Task Update([Inject] IUserDal dal, [Inject] IUnitOfWork work)
    {
        var staged = dal.StageUpdate(new UserDto(Id, DisplayName, Version));
        await work.CommitAsync();
        LoadVersion(staged.Version);
    }
}
