namespace CslaStarter.Core.Security;

/// <summary>
/// Role codes the platform itself depends on (bootstrap, admin-only screens). The business layer's
/// UserRoleType constants use these values; they must match the user_role_type seed data.
/// </summary>
public static class SystemRoles
{
    public const int Admin = 1;
}
