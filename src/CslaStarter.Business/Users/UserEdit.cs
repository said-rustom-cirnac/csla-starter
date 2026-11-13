using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;
using CslaStarter.Business.Rules;
using CslaStarter.Business.Types;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Core.Security;
using CslaStarter.Dal.Users;

namespace CslaStarter.Business.Users;

/// <summary>
/// A user as administrators manage them. Users are created by their first sign-in and never deleted, only deactivated.
/// Technical staff may view users, but not their employee number, and may not edit them.
/// </summary>
[Serializable]
public sealed class UserEdit : AppRootBase<UserEdit>
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> DisplayNameProperty = RegisterProperty<string>(nameof(DisplayName));
    public static readonly PropertyInfo<string?> EmailProperty = RegisterProperty<string?>(nameof(Email));
    public static readonly PropertyInfo<string> PreferredLanguageProperty = RegisterProperty<string>(nameof(PreferredLanguage));
    public static readonly PropertyInfo<bool> IsActiveProperty = RegisterProperty<bool>(nameof(IsActive));
    public static readonly PropertyInfo<string?> EmployeeNumberProperty = RegisterProperty<string?>(nameof(EmployeeNumber));
    public static readonly PropertyInfo<UserRoleAssignmentList> RolesProperty = RegisterProperty<UserRoleAssignmentList>(nameof(Roles));

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
        private set => LoadProperty(PreferredLanguageProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_IsActive))]
    public bool IsActive
    {
        get => GetProperty(IsActiveProperty);
        set => SetProperty(IsActiveProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_EmployeeNumber))]
    [Sensitive(RevealRoles = new[] { UserRoleType.Admin })]
    public string? EmployeeNumber
    {
        get => GetProperty(EmployeeNumberProperty);
        set => SetProperty(EmployeeNumberProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.User_Roles))]
    public UserRoleAssignmentList Roles
    {
        get => GetProperty(RolesProperty)!;
        private set => LoadProperty(RolesProperty, value);
    }

    public override string EntityType => "User";

    public override string EntityKey => Id.ToString();

    public static bool CanEdit(ApplicationContext applicationContext) =>
        BusinessRules.HasPermission(applicationContext, AuthorizationActions.EditObject, typeof(UserEdit));

    /// <summary>Gives the user a role (no change if they already hold it). Administrators only.</summary>
    public void AssignRole(int roleTypeCode)
    {
        if (!CanWriteProperty(RolesProperty))
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        Roles.Assign(roleTypeCode, ApplicationContext.GetRequiredService<IChildDataPortal<UserRoleAssignment>>());
    }

    /// <summary>Takes a role away. Administrators only.</summary>
    public void UnassignRole(int roleTypeCode)
    {
        if (!CanWriteProperty(RolesProperty))
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        Roles.Unassign(roleTypeCode);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(DisplayNameProperty, () => Messages.Common_Required));
        BusinessRules.AddRule(new MaxLength(DisplayNameProperty, 200, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new MaxLength(EmployeeNumberProperty, 20, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.ReadProperty, EmployeeNumberProperty, UserRoleType.Admin));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, EmployeeNumberProperty, UserRoleType.Admin));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, IsActiveProperty, UserRoleType.Admin));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, RolesProperty, UserRoleType.Admin));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, DisplayNameProperty, UserRoleType.Admin));
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(UserEdit), new HasRole(AuthorizationActions.GetObject, UserRoleType.Admin, UserRoleType.Technical));
        BusinessRules.AddRule(typeof(UserEdit), new HasRole(AuthorizationActions.EditObject, UserRoleType.Admin));
        BusinessRules.AddRule(typeof(UserEdit), new HasRole(AuthorizationActions.CreateObject));
        BusinessRules.AddRule(typeof(UserEdit), new HasRole(AuthorizationActions.DeleteObject));
    }

    [Fetch]
    private async Task Fetch(Guid id, [Inject] IUserDal dal, [Inject] IChildDataPortal<UserRoleAssignmentList> rolesPortal)
    {
        Load(await dal.FetchAsync(id, CancellationToken.None), rolesPortal);
        BusinessRules.CheckRules();
    }

    [Update]
    private async Task Update([Inject] IUserDal dal, [Inject] IChildDataPortal<UserRoleAssignmentList> rolesPortal)
    {
        var saved = await dal.UpdateAsync(
            new UserUpdateDto(Id, Version, ReadProperty(DisplayNameProperty)!, ReadProperty(IsActiveProperty), ReadProperty(EmployeeNumberProperty), [.. Roles.RoleCodes]),
            CancellationToken.None);
        Load(saved, rolesPortal);
    }

    private void Load(UserDto user, IChildDataPortal<UserRoleAssignmentList> rolesPortal)
    {
        using (BypassPropertyChecks)
        {
            Id = user.Id;
            DisplayName = user.DisplayName;
            Email = user.Email;
            PreferredLanguage = user.PreferredLanguage;
            IsActive = user.IsActive;
            EmployeeNumber = user.EmployeeNumber;
        }

        Roles = rolesPortal.FetchChild(user.RoleCodes);
        LoadVersion(user.Version);
        LoadAudit(user.CreatedOn, user.CreatedBy, user.ModifiedOn, user.ModifiedBy);
    }
}
