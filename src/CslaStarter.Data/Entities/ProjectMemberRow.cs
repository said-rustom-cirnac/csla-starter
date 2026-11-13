using CslaStarter.Data.Access;
using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

/// <summary>
/// A user who belongs to a project; membership is what gives a Client access to it. Not [RecordScoped] itself (the
/// project filter reads it); reach it only through a project.
/// </summary>
[AuditEntity("ProjectMember")]
[AccessGrant(nameof(UserId))]
public class ProjectMemberRow : IHasAuditRoot
{
    [AuditIgnore]
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public ProjectRow? Project { get; set; }

    public AppUser? User { get; set; }

    public string AuditRootType => "Project";

    public string AuditRootKey => ProjectId.ToString();
}
