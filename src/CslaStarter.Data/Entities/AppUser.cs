using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

/// <summary>A person who signs in through Entra ID, keyed on tenant id + object id.</summary>
[AuditEntity("User")]
public class AppUser : IAuditedEntity, IAggregateRoot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string TenantId { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PreferredLanguage { get; set; } = "en-CA";

    public bool IsActive { get; set; } = true;

    [AuditMask]
    public string? EmployeeNumber { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset ModifiedOn { get; set; }

    public string ModifiedBy { get; set; } = string.Empty;

    public uint Version { get; set; }

    public ICollection<UserRole> Roles { get; } = [];
}
