using CslaStarter.Dal;
using CslaStarter.Dal.Projects;
using CslaStarter.Data.Access;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Dal;

/// <summary>
/// The Project aggregate (sample feature). Every query goes through the "RecordAccess" filter, so a project the user
/// may not see is simply not found. Saves reconcile the whole aggregate in one SaveChanges and check the root version.
/// </summary>
public sealed class ProjectDal(AppDbContext db) : IProjectDal
{
    private static readonly Dictionary<string, string> UniqueIndexes = new(StringComparer.Ordinal) { ["ix_project_code"] = "Code" };

    public async Task<ProjectDto> FetchAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await LoadAsync(id, tracking: false, cancellationToken));

    public async Task<PagedResult<ProjectInfoDto>> FetchListAsync(ProjectListCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var projects = db.Projects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var pattern = DalHelpers.ContainsPattern(criteria.Search.Trim());
            projects = projects.Where(p => EF.Functions.ILike(p.Code, pattern) || EF.Functions.ILike(p.Name, pattern));
        }

        if (criteria.StatusCode is { } status)
        {
            projects = projects.Where(p => p.StatusCode == status);
        }

        projects = (criteria.SortBy, criteria.Descending) switch
        {
            ("Name", false) => projects.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ("Name", true) => projects.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            ("Status", false) => projects.OrderBy(p => p.StatusCode).ThenBy(p => p.Code),
            ("Status", true) => projects.OrderByDescending(p => p.StatusCode).ThenBy(p => p.Code),
            (_, true) => projects.OrderByDescending(p => p.Code),
            _ => projects.OrderBy(p => p.Code),
        };

        var total = await projects.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);
        var items = await projects
            .Skip((Math.Max(criteria.Page, 1) - 1) * pageSize).Take(pageSize)
            .Select(p => new ProjectInfoDto(p.Id, p.Code, p.Name, p.StatusCode, p.Budget, p.Tasks.Count(t => !t.IsDone), p.Tasks.Count))
            .ToListAsync(cancellationToken);
        return new PagedResult<ProjectInfoDto>(items, total);
    }

    public async Task<ProjectDto> InsertAsync(ProjectSaveDto project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var row = new ProjectRow { Id = project.Id };
        Apply(row, project);
        db.Projects.Add(row);
        await DalHelpers.SaveAggregateAsync(db, "Project", project.Id, cancellationToken, UniqueIndexes);
        return await FetchAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDto> UpdateAsync(ProjectSaveDto project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var row = await LoadAsync(project.Id, tracking: true, cancellationToken);
        DalHelpers.ExpectVersion(db, row, project.Version);
        Apply(row, project);
        DalHelpers.TouchRoot(db, row);
        await DalHelpers.SaveAggregateAsync(db, "Project", project.Id, cancellationToken, UniqueIndexes);
        return await FetchAsync(project.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken)
    {
        var row = await LoadAsync(id, tracking: true, cancellationToken);
        if (version is { } expected)
        {
            DalHelpers.ExpectVersion(db, row, expected);
        }

        db.Projects.Remove(row);
        await DalHelpers.SaveAggregateAsync(db, "Project", id, cancellationToken);
    }

    [BypassRecordAccess] // A code must be unique among all projects, including ones this user cannot see.
    public async Task<bool> IsCodeUniqueAsync(string code, Guid excludingId, CancellationToken cancellationToken) =>
        !await db.Projects.IgnoreQueryFilters([AppDbContext.RecordAccessFilter])
            .AnyAsync(p => p.Code == code && p.Id != excludingId, cancellationToken);

    public async Task<ProjectDto> CloseAsync(Guid id, uint version, int closedStatusCode, CancellationToken cancellationToken)
    {
        var row = await LoadAsync(id, tracking: true, cancellationToken);
        DalHelpers.ExpectVersion(db, row, version);
        row.StatusCode = closedStatusCode;
        await DalHelpers.SaveAggregateAsync(db, "Project", id, cancellationToken);
        return await FetchAsync(id, cancellationToken);
    }

    private async Task<ProjectRow> LoadAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = db.Projects.Include(p => p.Tasks).Include(p => p.Members).ThenInclude(m => m.User).AsSplitQuery();
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Project", id.ToString());
    }

    private static void Apply(ProjectRow row, ProjectSaveDto project)
    {
        row.Code = project.Code;
        row.Name = project.Name;
        row.Description = project.Description;
        row.StatusCode = project.StatusCode;
        row.Budget = project.Budget;
        row.StartDate = project.StartDate;
        row.EndDate = project.EndDate;

        var tasks = project.Tasks.ToDictionary(t => t.Id);
        foreach (var removed in row.Tasks.Where(t => !tasks.ContainsKey(t.Id)).ToList())
        {
            row.Tasks.Remove(removed);
        }

        foreach (var task in project.Tasks)
        {
            var existing = row.Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing is null)
            {
                existing = new ProjectTaskRow { Id = task.Id };
                row.Tasks.Add(existing);
            }

            existing.Title = task.Title;
            existing.IsDone = task.IsDone;
            existing.DueDate = task.DueDate;
            existing.SortOrder = task.SortOrder;
        }

        foreach (var removed in row.Members.Where(m => !project.MemberUserIds.Contains(m.UserId)).ToList())
        {
            row.Members.Remove(removed);
        }

        foreach (var added in project.MemberUserIds.Except(row.Members.Select(m => m.UserId)).ToList())
        {
            row.Members.Add(new ProjectMemberRow { UserId = added });
        }
    }

    private static ProjectDto Map(ProjectRow p) => new(
        p.Id, p.Code, p.Name, p.Description, p.StatusCode, p.Budget, p.StartDate, p.EndDate,
        p.Tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).Select(t => new ProjectTaskDto(t.Id, t.Title, t.IsDone, t.DueDate, t.SortOrder)).ToList(),
        p.Members.OrderBy(m => m.User?.DisplayName).Select(m => new ProjectMemberDto(m.UserId, m.User?.DisplayName ?? string.Empty)).ToList(),
        p.Version, p.CreatedOn, p.CreatedBy, p.ModifiedOn, p.ModifiedBy);
}
