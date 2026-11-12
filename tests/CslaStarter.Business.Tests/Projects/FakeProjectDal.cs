using CslaStarter.Dal;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Tests.Projects;

public sealed class FakeProjectDal : IProjectDal
{
    public Dictionary<Guid, ProjectDto> Projects { get; } = [];

    public HashSet<string> TakenCodes { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, code checks wait for it, so a test can see the "Checking…" busy state.</summary>
    public TaskCompletionSource? CodeCheckGate { get; set; }

    public ProjectSaveDto? LastSaved { get; private set; }

    public ProjectDto Add(Guid id, string code, int status = 2, decimal? budget = 1000m, IReadOnlyList<ProjectTaskDto>? tasks = null, params Guid[] members)
    {
        var project = new ProjectDto(id, code, code, null, status, budget, null, null, tasks ?? [], members.Select(m => new ProjectMemberDto(m, "Member")).ToList(), 5,
            DateTimeOffset.UnixEpoch, "seed", DateTimeOffset.UnixEpoch, "seed");
        Projects[id] = project;
        return project;
    }

    public Task<ProjectDto> FetchAsync(Guid id, CancellationToken cancellationToken) =>
        Projects.TryGetValue(id, out var project) ? Task.FromResult(project) : throw new NotFoundException("Project", id.ToString());

    public Task<PagedResult<ProjectInfoDto>> FetchListAsync(ProjectListCriteria criteria, CancellationToken cancellationToken)
    {
        var items = Projects.Values.Select(p => new ProjectInfoDto(p.Id, p.Code, p.Name, p.StatusCode, p.Budget, p.Tasks.Count(t => !t.IsDone), p.Tasks.Count)).ToList();
        return Task.FromResult(new PagedResult<ProjectInfoDto>(items, items.Count));
    }

    public Task<ProjectDto> InsertAsync(ProjectSaveDto project, CancellationToken cancellationToken) => Save(project, 1);

    public Task<ProjectDto> UpdateAsync(ProjectSaveDto project, CancellationToken cancellationToken)
    {
        if (Projects[project.Id].Version != project.Version)
        {
            throw new ConcurrencyException("Project", project.Id.ToString());
        }

        return Save(project, project.Version + 1);
    }

    public Task DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken)
    {
        Projects.Remove(id);
        return Task.CompletedTask;
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid excludingId, CancellationToken cancellationToken)
    {
        if (CodeCheckGate is not null)
        {
            await CodeCheckGate.Task;
        }

        return !TakenCodes.Contains(code) && !Projects.Values.Any(p => p.Code == code && p.Id != excludingId);
    }

    public Task<ProjectDto> CloseAsync(Guid id, int closedStatusCode, CancellationToken cancellationToken)
    {
        Projects[id] = Projects[id] with { StatusCode = closedStatusCode, Version = Projects[id].Version + 1 };
        return Task.FromResult(Projects[id]);
    }

    private Task<ProjectDto> Save(ProjectSaveDto project, uint version)
    {
        LastSaved = project;
        var dto = new ProjectDto(project.Id, project.Code, project.Name, project.Description, project.StatusCode, project.Budget, project.StartDate, project.EndDate,
            project.Tasks, project.MemberUserIds.Select(m => new ProjectMemberDto(m, "Member")).ToList(), version, DateTimeOffset.UnixEpoch, "t", DateTimeOffset.UnixEpoch, "t");
        Projects[project.Id] = dto;
        return Task.FromResult(dto);
    }
}
