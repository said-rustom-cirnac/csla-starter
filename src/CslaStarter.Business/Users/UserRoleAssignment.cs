using Csla;
using CslaStarter.Business.Auditing;
using CslaStarter.Business.Base;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;

namespace CslaStarter.Business.Users;

/// <summary>One role a user holds (a UserRoleType code).</summary>
[Serializable]
[HistoryOf("UserRole")]
public sealed class UserRoleAssignment : AppBusinessBase<UserRoleAssignment>
{
    public static readonly PropertyInfo<int> RoleTypeCodeProperty = RegisterProperty<int>(nameof(RoleTypeCode));

    [LocalizedDisplayName(nameof(Labels.User_RoleType))]
    public int RoleTypeCode
    {
        get => GetProperty(RoleTypeCodeProperty);
        private set => LoadProperty(RoleTypeCodeProperty, value);
    }

    public override string EntityType => "UserRole";

    public override string EntityKey => RoleTypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [CreateChild]
    private void CreateChild(int roleTypeCode) => RoleTypeCode = roleTypeCode;

    [FetchChild]
    private void FetchChild(int roleTypeCode) => RoleTypeCode = roleTypeCode;
}
