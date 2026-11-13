namespace CslaStarter.Data.Entities;

/// <summary>A row of the project_status_type lookup. Codes are owned by ProjectStatusType in the business layer.</summary>
public class ProjectStatusTypeRow
{
    public int Code { get; set; }

    public string DescriptionEn { get; set; } = string.Empty;

    public string DescriptionFr { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
