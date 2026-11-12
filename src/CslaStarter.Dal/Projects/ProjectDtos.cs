namespace CslaStarter.Dal.Projects;

public sealed record ProjectTaskDto(Guid Id, string Title, bool IsDone, DateOnly? DueDate, int SortOrder);

public sealed record ProjectMemberDto(Guid UserId, string DisplayName);

public sealed record ProjectDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int StatusCode,
    decimal? Budget,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ProjectTaskDto> Tasks,
    IReadOnlyList<ProjectMemberDto> Members,
    uint Version,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset ModifiedOn,
    string ModifiedBy);

/// <summary>The whole aggregate as the business layer saves it: children are matched by key (missing ones are removed).</summary>
public sealed record ProjectSaveDto(
    Guid Id,
    uint Version,
    string Code,
    string Name,
    string? Description,
    int StatusCode,
    decimal? Budget,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ProjectTaskDto> Tasks,
    IReadOnlyList<Guid> MemberUserIds);

public sealed record ProjectInfoDto(Guid Id, string Code, string Name, int StatusCode, decimal? Budget, int OpenTaskCount, int TaskCount);

/// <summary><c>SortBy</c> is "Code", "Name" or "Status".</summary>
public sealed record ProjectListCriteria(string? Search, int? StatusCode, string SortBy, bool Descending, int Page, int PageSize);
