namespace CslaStarter.Dal.Users;

public sealed record UserDto(
    Guid Id,
    string TenantId,
    string ObjectId,
    string DisplayName,
    string? Email,
    string PreferredLanguage,
    bool IsActive,
    string? EmployeeNumber,
    IReadOnlyList<int> RoleCodes,
    uint Version,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset ModifiedOn,
    string ModifiedBy);

/// <summary>What an administrator may change on a user. Roles are replaced as a set.</summary>
public sealed record UserUpdateDto(Guid Id, uint Version, string DisplayName, bool IsActive, string? EmployeeNumber, IReadOnlyList<int> RoleCodes);

/// <summary>What users may change about themselves.</summary>
public sealed record UserProfileUpdateDto(Guid Id, uint Version, string DisplayName, string PreferredLanguage);

public sealed record UserListCriteria(string? Search = null, int? RoleCode = null, int Page = 1, int PageSize = 25);

public sealed record UserInfoDto(Guid Id, string DisplayName, string? Email, bool IsActive, IReadOnlyList<int> RoleCodes);
