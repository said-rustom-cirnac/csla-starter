namespace CslaStarter.Business.Types;

/// <summary>Where a project stands. Codes must match the project_status_type seed rows (checked by a test).</summary>
[Serializable]
public sealed class ProjectStatusType : CslaType<ProjectStatusType>
{
    public const int Planned = 1;
    public const int Active = 2;
    public const int OnHold = 3;
    public const int Closed = 4;
}

[Serializable]
public sealed class ProjectStatusTypeList : CslaTypeList<ProjectStatusTypeList, ProjectStatusType>;
