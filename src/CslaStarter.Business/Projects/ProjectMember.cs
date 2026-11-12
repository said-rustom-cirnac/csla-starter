using Csla;
using CslaStarter.Business.Base;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Business.Projects;

/// <summary>A user who belongs to a project (which is what lets a Client see it).</summary>
[Serializable]
public sealed class ProjectMember : AppBusinessBase<ProjectMember>
{
    public static readonly PropertyInfo<Guid> UserIdProperty = RegisterProperty<Guid>(nameof(UserId));
    public static readonly PropertyInfo<string> DisplayNameProperty = RegisterProperty<string>(nameof(DisplayName));

    [LocalizedDisplayName(nameof(Labels.Member_User))]
    public Guid UserId
    {
        get => GetProperty(UserIdProperty);
        private set => LoadProperty(UserIdProperty, value);
    }

    [LocalizedDisplayName(nameof(Labels.Member_DisplayName))]
    public string DisplayName
    {
        get => GetProperty(DisplayNameProperty)!;
        private set => LoadProperty(DisplayNameProperty, value);
    }

    public override string EntityType => "ProjectMember";

    public override string EntityKey => UserId.ToString();

    [CreateChild]
    private void CreateChild(Guid userId, string displayName)
    {
        UserId = userId;
        DisplayName = displayName;
    }

    [FetchChild]
    private void FetchChild(ProjectMemberDto member)
    {
        UserId = member.UserId;
        DisplayName = member.DisplayName;
    }
}
