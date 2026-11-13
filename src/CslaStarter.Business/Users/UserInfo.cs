using Csla;
using CslaStarter.Core;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Users;

namespace CslaStarter.Business.Users;

/// <summary>One row of the user list.</summary>
[Serializable]
public sealed class UserInfo : ReadOnlyBase<UserInfo>, IEntityIdentity
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> DisplayNameProperty = RegisterProperty<string>(nameof(DisplayName));
    public static readonly PropertyInfo<string?> EmailProperty = RegisterProperty<string?>(nameof(Email));
    public static readonly PropertyInfo<bool> IsActiveProperty = RegisterProperty<bool>(nameof(IsActive));
    public static readonly PropertyInfo<List<int>> RoleCodesProperty = RegisterProperty<List<int>>(nameof(RoleCodes));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public Guid Id => GetProperty(IdProperty);

    [LocalizedDisplayName(nameof(Labels.User_DisplayName))]
    public string DisplayName => GetProperty(DisplayNameProperty)!;

    [LocalizedDisplayName(nameof(Labels.User_Email))]
    public string? Email => GetProperty(EmailProperty);

    [LocalizedDisplayName(nameof(Labels.User_IsActive))]
    public bool IsActive => GetProperty(IsActiveProperty);

    [LocalizedDisplayName(nameof(Labels.User_Roles))]
    public IReadOnlyList<int> RoleCodes => GetProperty(RoleCodesProperty)!;

    public string EntityType => "User";

    public string EntityKey => Id.ToString();

    [FetchChild]
    private void FetchChild(UserInfoDto row)
    {
        LoadProperty(IdProperty, row.Id);
        LoadProperty(DisplayNameProperty, row.DisplayName);
        LoadProperty(EmailProperty, row.Email);
        LoadProperty(IsActiveProperty, row.IsActive);
        LoadProperty(RoleCodesProperty, row.RoleCodes.ToList());
    }
}
