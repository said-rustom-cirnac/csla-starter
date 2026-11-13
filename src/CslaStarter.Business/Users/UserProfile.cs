using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;
using CslaStarter.Business.Rules;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Core.Security;
using CslaStarter.Dal.Users;

namespace CslaStarter.Business.Users;

/// <summary>
/// The signed-in user's own settings. It can only ever be fetched for the signed-in user, so nobody can read or change
/// anyone else's profile through it.
/// </summary>
[Serializable]
public sealed class UserProfile : AppRootBase<UserProfile>
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> DisplayNameProperty = RegisterProperty<string>(nameof(DisplayName));
    public static readonly PropertyInfo<string?> EmailProperty = RegisterProperty<string?>(nameof(Email));
    public static readonly PropertyInfo<string> PreferredLanguageProperty = RegisterProperty<string>(nameof(PreferredLanguage));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public Guid Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_DisplayName))]
    public string DisplayName
    {
        get => GetProperty(DisplayNameProperty)!;
        set => SetProperty(DisplayNameProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_Email))]
    public string? Email
    {
        get => GetProperty(EmailProperty);
        private set => LoadProperty(EmailProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_PreferredLanguage))]
    public string PreferredLanguage
    {
        get => GetProperty(PreferredLanguageProperty)!;
        set => SetProperty(PreferredLanguageProperty, value);
    }

    public override string EntityType => "User";

    public override string EntityKey => Id.ToString();

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(DisplayNameProperty, () => Messages.Common_Required));
        BusinessRules.AddRule(new MaxLength(DisplayNameProperty, 200, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new SupportedLanguage(PreferredLanguageProperty, () => Messages.User_UnsupportedLanguage));
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserProfile), new IsSignedIn(AuthorizationActions.GetObject));
        BusinessRules.AddRule(typeof(UserProfile), new IsSignedIn(AuthorizationActions.EditObject));
        BusinessRules.AddRule(typeof(UserProfile), new HasRole(AuthorizationActions.CreateObject));
        BusinessRules.AddRule(typeof(UserProfile), new HasRole(AuthorizationActions.DeleteObject));
    }

    [Fetch]
    private async Task Fetch([Inject] ICurrentUser currentUser, [Inject] IUserDal dal)
    {
        var id = currentUser.UserId ?? throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        Load(await dal.FetchAsync(id, CancellationToken.None));
        BusinessRules.CheckRules();
    }

    [Update]
    private async Task Update([Inject] ICurrentUser currentUser, [Inject] IUserDal dal)
    {
        if (currentUser.UserId != Id)
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        Load(await dal.UpdateProfileAsync(
            new UserProfileUpdateDto(Id, Version, ReadProperty(DisplayNameProperty)!, ReadProperty(PreferredLanguageProperty)!),
            CancellationToken.None));
    }

    private void Load(UserDto user)
    {
        using (BypassPropertyChecks)
        {
            Id = user.Id;
            DisplayName = user.DisplayName;
            Email = user.Email;
            PreferredLanguage = user.PreferredLanguage;
        }

        LoadVersion(user.Version);
        LoadAudit(user.CreatedOn, user.CreatedBy, user.ModifiedOn, user.ModifiedBy);
    }
}
