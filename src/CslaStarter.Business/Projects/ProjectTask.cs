using Csla;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>One task of a project.</summary>
[Serializable]
public sealed class ProjectTask : AppBusinessBase<ProjectTask>
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> TitleProperty = RegisterProperty<string>(nameof(Title));
    public static readonly PropertyInfo<bool> IsDoneProperty = RegisterProperty<bool>(nameof(IsDone));
    public static readonly PropertyInfo<DateOnly?> DueDateProperty = RegisterProperty<DateOnly?>(nameof(DueDate));
    public static readonly PropertyInfo<int> SortOrderProperty = RegisterProperty<int>(nameof(SortOrder));

    [LocalizedDisplayName(nameof(Labels.Common_Id))]
    public Guid Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Task_Title))]
    public string Title
    {
        get => GetProperty(TitleProperty)!;
        set => SetProperty(TitleProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Task_IsDone))]
    public bool IsDone
    {
        get => GetProperty(IsDoneProperty);
        set => SetProperty(IsDoneProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Task_DueDate))]
    public DateOnly? DueDate
    {
        get => GetProperty(DueDateProperty);
        set => SetProperty(DueDateProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Task_SortOrder))]
    public int SortOrder
    {
        get => GetProperty(SortOrderProperty);
        set => SetProperty(SortOrderProperty, value);
    }

    public override string EntityType => "ProjectTask";

    public override string EntityKey => Id.ToString();

    internal ProjectTaskDto ToDto() => new(Id, ReadProperty(TitleProperty)!, ReadProperty(IsDoneProperty), ReadProperty(DueDateProperty), ReadProperty(SortOrderProperty));

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(TitleProperty, () => Messages.Common_Required));
        BusinessRules.AddRule(new MaxLength(TitleProperty, 200, () => Messages.Common_MaxLength));
    }

    [CreateChild]
    private void CreateChild(string title, int sortOrder)
    {
        using (BypassPropertyChecks)
        {
            Id = Guid.CreateVersion7();
            Title = title;
            SortOrder = sortOrder;
        }

        BusinessRules.CheckRules();
    }

    [FetchChild]
    private void FetchChild(ProjectTaskDto task)
    {
        using (BypassPropertyChecks)
        {
            Id = task.Id;
            Title = task.Title;
            IsDone = task.IsDone;
            DueDate = task.DueDate;
            SortOrder = task.SortOrder;
        }
    }
}
