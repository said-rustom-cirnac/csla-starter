using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

/// <summary>One role assignment. A user can hold several roles.</summary>
[AuditEntity("UserRole")]
public class UserRole : IHasAuditRoot
{
    [AuditIgnore]
    public Guid UserId { get; set; }

    [AuditTypeCode("UserRoleType")]
    public int RoleTypeCode { get; set; }

    public AppUser? User { get; set; }

    public string AuditRootType => "User";

    public string AuditRootKey => UserId.ToString();
}
