using CslaStarter.Core.Security;

namespace CslaStarter.Business.Types;

/// <summary>A user's role. Codes must match the user_role_type seed rows (checked by a test).</summary>
[Serializable]
public sealed class UserRoleType : CslaType<UserRoleType>
{
    public const int Admin = SystemRoles.Admin;
    public const int Client = 2;
    public const int Technical = 3;
}

[Serializable]
public sealed class UserRoleTypeList : CslaTypeList<UserRoleTypeList, UserRoleType>;
