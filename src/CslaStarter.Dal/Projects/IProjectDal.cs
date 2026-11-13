namespace CslaStarter.Dal.Projects;

/// <summary>Projects the current user may see (record-level access applies to every method except the code check).</summary>
public interface IProjectDal
{
    /// <exception cref="NotFoundException">No such project, or the user may not see it.</exception>
    Task<ProjectDto> FetchAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ProjectInfoDto>> FetchListAsync(ProjectListCriteria criteria, CancellationToken cancellationToken);

    Task<ProjectDto> InsertAsync(ProjectSaveDto project, CancellationToken cancellationToken);

    /// <exception cref="ConcurrencyException">Any part of the project changed since <see cref="ProjectSaveDto.Version"/>.</exception>
    Task<ProjectDto> UpdateAsync(ProjectSaveDto project, CancellationToken cancellationToken);

    /// <summary>Deletes the project and its tasks and members. <c>version</c> is the one the user loaded, or null for whatever is current.</summary>
    Task DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken);

    /// <summary>Codes are unique across all projects, including ones the user cannot see.</summary>
    Task<bool> IsCodeUniqueAsync(string code, Guid excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the project's status to <c>closedStatusCode</c> (ProjectStatusType.Closed, owned by the business layer), if
    /// the project is still at <paramref name="version"/>: otherwise <see cref="ConcurrencyException"/>, so a task added
    /// after the caller checked for open tasks stops the close.
    /// </summary>
    Task<ProjectDto> CloseAsync(Guid id, uint version, int closedStatusCode, CancellationToken cancellationToken);
}
