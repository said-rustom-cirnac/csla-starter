namespace CslaStarter.Data.Entities;

/// <summary>A row of the user_role_type lookup. Codes are owned by UserRoleType in the business layer.</summary>
public class UserRoleTypeRow
{
    public int Code { get; set; }

    public string DescriptionEn { get; set; } = string.Empty;

    public string DescriptionFr { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
