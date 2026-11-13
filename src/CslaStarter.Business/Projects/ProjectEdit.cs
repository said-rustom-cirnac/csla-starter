using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;
using CslaStarter.Business.Projects.Rules;
using CslaStarter.Business.Rules;
using CslaStarter.Business.Types;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>
/// A project with its tasks and members (sample feature). Clients see only projects they belong to (record-level
/// access) and cannot edit; Technical staff edit but cannot change the budget; Admins do everything.
/// </summary>
[Serializable]
public sealed class ProjectEdit : AppRootBase<ProjectEdit>
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> CodeProperty = RegisterProperty<string>(nameof(Code));
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public static readonly PropertyInfo<string?> DescriptionProperty = RegisterProperty<string?>(nameof(Description));
    public static readonly PropertyInfo<int> StatusCodeProperty = RegisterProperty<int>(nameof(StatusCode));
    public static readonly PropertyInfo<int> OriginalStatusCodeProperty = RegisterProperty<int>(nameof(OriginalStatusCode));
    public static readonly PropertyInfo<decimal?> BudgetProperty = RegisterProperty<decimal?>(nameof(Budget));
    public static readonly PropertyInfo<DateOnly?> StartDateProperty = RegisterProperty<DateOnly?>(nameof(StartDate));
    public static readonly PropertyInfo<DateOnly?> EndDateProperty = RegisterProperty<DateOnly?>(nameof(EndDate));
    public static readonly PropertyInfo<ProjectTaskList> TasksProperty = RegisterProperty<ProjectTaskList>(nameof(Tasks));
    public static readonly PropertyInfo<ProjectMemberList> MembersProperty = RegisterProperty<ProjectMemberList>(nameof(Members));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public Guid Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Code))]
    public string Code
    {
        get => GetProperty(CodeProperty)!;
        set => SetProperty(CodeProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Name))]
    public string Name
    {
        get => GetProperty(NameProperty)!;
        set => SetProperty(NameProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Description))]
    public string? Description
    {
        get => GetProperty(DescriptionProperty);
        set => SetProperty(DescriptionProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Status))]
    public int StatusCode
    {
        get => GetProperty(StatusCodeProperty);
        set => SetProperty(StatusCodeProperty, value);
    }

    /// <summary>The status when the project was loaded (0 when new): rules allow keeping it even if since retired.</summary>
    [LocalizedDisplayName(nameof(Labels.Project_Status))]
    public int OriginalStatusCode
    {
        get => GetProperty(OriginalStatusCodeProperty);
        private set => LoadProperty(OriginalStatusCodeProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Budget))]
    public decimal? Budget
    {
        get => GetProperty(BudgetProperty);
        set => SetProperty(BudgetProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_StartDate))]
    public DateOnly? StartDate
    {
        get => GetProperty(StartDateProperty);
        set => SetProperty(StartDateProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_EndDate))]
    public DateOnly? EndDate
    {
        get => GetProperty(EndDateProperty);
        set => SetProperty(EndDateProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Tasks))]
    public ProjectTaskList Tasks
    {
        get => GetProperty(TasksProperty)!;
        private set => LoadProperty(TasksProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Project_Members))]
    public ProjectMemberList Members
    {
        get => GetProperty(MembersProperty)!;
        private set => LoadProperty(MembersProperty, value);
    }

    public override string EntityType => "Project";

    public override string EntityKey => Id.ToString();

    public static bool CanCreate(ApplicationContext applicationContext) =>
        BusinessRules.HasPermission(applicationContext, AuthorizationActions.CreateObject, typeof(ProjectEdit));

    public static bool CanEdit(ApplicationContext applicationContext) =>
        BusinessRules.HasPermission(applicationContext, AuthorizationActions.EditObject, typeof(ProjectEdit));

    public ProjectTask AddTask(string title) =>
        Tasks.AddTask(title, ApplicationContext.GetRequiredService<IChildDataPortal<ProjectTask>>());

    /// <summary>Adds a member (no change if already a member). Administrators only.</summary>
    public void AddMember(Guid userId, string displayName)
    {
        if (!CanWriteProperty(MembersProperty))
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        Members.AddMember(userId, displayName, ApplicationContext.GetRequiredService<IChildDataPortal<ProjectMember>>());
    }

    /// <summary>Removes a member. Administrators only.</summary>
    public void RemoveMember(Guid userId)
    {
        if (!CanWriteProperty(MembersProperty))
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        Members.RemoveMember(userId);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(CodeProperty, () => Messages.Common_Required));
        BusinessRules.AddRule(new MaxLength(CodeProperty, 20, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new RegExMatch(CodeProperty, "^[A-Z0-9-]+$", () => Messages.Project_CodeFormat));
        BusinessRules.AddRule(new UniqueProjectCode(CodeProperty, IdProperty, () => Messages.Project_CodeTaken));
        BusinessRules.AddRule(new Required(NameProperty, () => Messages.Common_Required));
        BusinessRules.AddRule(new MaxLength(NameProperty, 200, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new MaxLength(DescriptionProperty, 2000, () => Messages.Common_MaxLength));
        BusinessRules.AddRule(new ActiveStatus(StatusCodeProperty, OriginalStatusCodeProperty, () => Messages.Project_InvalidStatus));
        BusinessRules.AddRule(new ClosedByCommandOnly(StatusCodeProperty, OriginalStatusCodeProperty, ProjectStatusType.Closed, () => Messages.Project_CloseWithCommand));
        BusinessRules.AddRule(new NotNegative(BudgetProperty, () => Messages.Project_BudgetNegative));
        BusinessRules.AddRule(new EndAfterStart(EndDateProperty, StartDateProperty, () => Messages.Project_EndBeforeStart));
        BusinessRules.AddRule(new Dependency(StartDateProperty, EndDateProperty));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.ReadProperty, BudgetProperty, UserRoleType.Admin, UserRoleType.Technical));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, BudgetProperty, UserRoleType.Admin));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, MembersProperty, UserRoleType.Admin));
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(ProjectEdit), new IsSignedIn(AuthorizationActions.GetObject));
        BusinessRules.AddRule(typeof(ProjectEdit), new HasRole(AuthorizationActions.CreateObject, UserRoleType.Admin, UserRoleType.Technical));
        BusinessRules.AddRule(typeof(ProjectEdit), new HasRole(AuthorizationActions.EditObject, UserRoleType.Admin, UserRoleType.Technical));
        BusinessRules.AddRule(typeof(ProjectEdit), new HasRole(AuthorizationActions.DeleteObject, UserRoleType.Admin));
    }

    [Create]
    private void Create([Inject] IChildDataPortal<ProjectTaskList> tasksPortal, [Inject] IChildDataPortal<ProjectMemberList> membersPortal)
    {
        using (BypassPropertyChecks)
        {
            Id = Guid.CreateVersion7();
            StatusCode = ProjectStatusType.Planned;
        }

        Tasks = tasksPortal.FetchChild((IReadOnlyList<ProjectTaskDto>)[]);
        Members = membersPortal.FetchChild((IReadOnlyList<ProjectMemberDto>)[]);
        BusinessRules.CheckRules();
    }

    [Fetch]
    private async Task Fetch(Guid id, [Inject] IProjectDal dal, [Inject] IChildDataPortal<ProjectTaskList> tasksPortal, [Inject] IChildDataPortal<ProjectMemberList> membersPortal)
    {
        Load(await dal.FetchAsync(id, CancellationToken.None), tasksPortal, membersPortal);
        BusinessRules.CheckRules();
    }

    [Insert]
    private async Task Insert([Inject] IProjectDal dal, [Inject] IChildDataPortal<ProjectTaskList> tasksPortal, [Inject] IChildDataPortal<ProjectMemberList> membersPortal) =>
        Load(await SaveAsync(dto => dal.InsertAsync(dto, CancellationToken.None)), tasksPortal, membersPortal);

    [Update]
    private async Task Update([Inject] IProjectDal dal, [Inject] IChildDataPortal<ProjectTaskList> tasksPortal, [Inject] IChildDataPortal<ProjectMemberList> membersPortal) =>
        Load(await SaveAsync(dto => dal.UpdateAsync(dto, CancellationToken.None)), tasksPortal, membersPortal);

    [DeleteSelf]
    private Task DeleteSelf([Inject] IProjectDal dal) => dal.DeleteAsync(Id, Version, CancellationToken.None);

    [Delete]
    private Task Delete(Guid id, [Inject] IProjectDal dal)
    {
        Id = id;
        return dal.DeleteAsync(Id, null, CancellationToken.None);
    }

    // The member list can be changed directly (Members.Remove...), so the permission is checked again when saving. A
    // duplicate code that slipped past the rule (it doesn't run on the server) fails on the unique index instead.
    private async Task<ProjectDto> SaveAsync(Func<ProjectSaveDto, Task<ProjectDto>> save)
    {
        if (Members.IsDirty && !CanWriteProperty(MembersProperty))
        {
            throw new Csla.Security.SecurityException(Messages.AccessDenied_Title);
        }

        try
        {
            return await save(ToSaveDto());
        }
        catch (DuplicateValueException e) when (e.Property == nameof(Code))
        {
            throw new ValidationException(RuleMessages.Format(() => Messages.Project_CodeTaken, CodeProperty.FriendlyName), e);
        }
    }

    private ProjectSaveDto ToSaveDto() => new(
        Id, Version, ReadProperty(CodeProperty)!, ReadProperty(NameProperty)!, ReadProperty(DescriptionProperty), ReadProperty(StatusCodeProperty),
        ReadProperty(BudgetProperty), ReadProperty(StartDateProperty), ReadProperty(EndDateProperty),
        [.. Tasks.Select(t => t.ToDto())], [.. Members.Select(m => m.UserId)]);

    private void Load(ProjectDto project, IChildDataPortal<ProjectTaskList> tasksPortal, IChildDataPortal<ProjectMemberList> membersPortal)
    {
        using (BypassPropertyChecks)
        {
            Id = project.Id;
            Code = project.Code;
            Name = project.Name;
            Description = project.Description;
            StatusCode = project.StatusCode;
            OriginalStatusCode = project.StatusCode;
            Budget = project.Budget;
            StartDate = project.StartDate;
            EndDate = project.EndDate;
        }

        Tasks = tasksPortal.FetchChild(project.Tasks);
        Members = membersPortal.FetchChild(project.Members);
        LoadVersion(project.Version);
        LoadAudit(project.CreatedOn, project.CreatedBy, project.ModifiedOn, project.ModifiedBy);
    }
}
