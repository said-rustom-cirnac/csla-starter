namespace CslaStarter.Data.Entities;

/// <summary>One role assignment. A user can hold several roles.</summary>
public class UserRole
{
    public Guid UserId { get; set; }

    public int RoleTypeCode { get; set; }

    public AppUser? User { get; set; }
}
