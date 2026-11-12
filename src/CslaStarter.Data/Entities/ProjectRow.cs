using CslaStarter.Data.Access;
using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

/// <summary>The root of the Project aggregate (sample feature).</summary>
[AuditEntity("Project")]
[RecordScoped]
public class ProjectRow : IAuditedEntity, IAggregateRoot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [AuditTypeCode("ProjectStatusType")]
    public int StatusCode { get; set; }

    public decimal? Budget { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset ModifiedOn { get; set; }

    public string ModifiedBy { get; set; } = string.Empty;

    public uint Version { get; set; }

    public ICollection<ProjectTaskRow> Tasks { get; } = [];

    public ICollection<ProjectMemberRow> Members { get; } = [];
}
