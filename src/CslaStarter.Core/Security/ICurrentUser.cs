namespace CslaStarter.Core.Security;

/// <summary>The signed-in user, as business rules and components see them. Roles are codes of UserRoleType.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string DisplayName { get; }

    IReadOnlySet<int> RoleCodes { get; }

    string PreferredLanguage { get; }

    bool HasRole(int roleCode);

    bool HasAnyRole(params int[] roleCodes);
}
