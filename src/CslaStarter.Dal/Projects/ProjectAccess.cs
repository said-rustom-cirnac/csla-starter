namespace CslaStarter.Dal.Projects;

/// <summary>
/// Who sees which projects, shared by the data layer (query filter) and the business layer (per-record rules):
/// these roles see every project; anyone else sees only projects where they are a member.
/// </summary>
public static class ProjectAccess
{
    /// <summary>UserRoleType.Admin (1) and UserRoleType.Technical (3); a test keeps them in step with the business constants.</summary>
    public static IReadOnlySet<int> SeesAllRoles { get; } = new HashSet<int> { 1, 3 };
}
