using CslaStarter.Data.Access;
using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

[AuditEntity("ProjectTask")]
[RecordScoped]
public class ProjectTaskRow : IHasAuditRoot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [AuditIgnore]
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    public DateOnly? DueDate { get; set; }

    public int SortOrder { get; set; }

    public ProjectRow? Project { get; set; }

    public string AuditRootType => "Project";

    public string AuditRootKey => ProjectId.ToString();
}
