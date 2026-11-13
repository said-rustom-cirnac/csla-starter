using Csla;
using Csla.Rules;
using CslaStarter.Business.Rules;
using CslaStarter.Business.Types;
using CslaStarter.Core;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>One row of the project list. The budget follows the same read rule as on ProjectEdit.</summary>
[Serializable]
public sealed class ProjectInfo : ReadOnlyBase<ProjectInfo>, IEntityIdentity
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> CodeProperty = RegisterProperty<string>(nameof(Code));
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public static readonly PropertyInfo<int> StatusCodeProperty = RegisterProperty<int>(nameof(StatusCode));
    public static readonly PropertyInfo<decimal?> BudgetProperty = RegisterProperty<decimal?>(nameof(Budget));
    public static readonly PropertyInfo<int> OpenTaskCountProperty = RegisterProperty<int>(nameof(OpenTaskCount));
    public static readonly PropertyInfo<int> TaskCountProperty = RegisterProperty<int>(nameof(TaskCount));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public Guid Id => GetProperty(IdProperty);

    [LocalizedDisplayName(nameof(Labels.Project_Code))]
    public string Code => GetProperty(CodeProperty)!;

    [LocalizedDisplayName(nameof(Labels.Project_Name))]
    public string Name => GetProperty(NameProperty)!;

    [LocalizedDisplayName(nameof(Labels.Project_Status))]
    public int StatusCode => GetProperty(StatusCodeProperty);

    [LocalizedDisplayName(nameof(Labels.Project_Budget))]
    public decimal? Budget => GetProperty(BudgetProperty);

    [LocalizedDisplayName(nameof(Labels.Project_OpenTaskCount))]
    public int OpenTaskCount => GetProperty(OpenTaskCountProperty);

    [LocalizedDisplayName(nameof(Labels.Project_TaskCount))]
    public int TaskCount => GetProperty(TaskCountProperty);

    public string EntityType => "Project";

    public string EntityKey => Id.ToString();

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new HasRole(AuthorizationActions.ReadProperty, BudgetProperty, UserRoleType.Admin, UserRoleType.Technical));
    }

    [FetchChild]
    private void FetchChild(ProjectInfoDto row)
    {
        LoadProperty(IdProperty, row.Id);
        LoadProperty(CodeProperty, row.Code);
        LoadProperty(NameProperty, row.Name);
        LoadProperty(StatusCodeProperty, row.StatusCode);
        LoadProperty(BudgetProperty, row.Budget);
        LoadProperty(OpenTaskCountProperty, row.OpenTaskCount);
        LoadProperty(TaskCountProperty, row.TaskCount);
    }
}
