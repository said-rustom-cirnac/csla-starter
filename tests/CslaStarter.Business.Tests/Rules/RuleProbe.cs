using Csla;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;
using CslaStarter.Core.Localization;

namespace CslaStarter.Business.Tests.Rules;

/// <summary>A minimal root used to test the base class helpers.</summary>
[Serializable]
public sealed class RuleProbe : AppRootBase<RuleProbe>
{
    public static readonly PropertyInfo<Guid> IdProperty = RegisterProperty<Guid>(nameof(Id));
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public static readonly PropertyInfo<string> NoteProperty = RegisterProperty<string>(nameof(Note));

    [LocalizedDisplayName("Common_Save")]
    public Guid Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    [LocalizedDisplayName("Common_Save")]
    public string Name
    {
        get => GetProperty(NameProperty)!;
        set => SetProperty(NameProperty, value);
    }

    [LocalizedDisplayName("Common_Save")]
    public string Note
    {
        get => GetProperty(NoteProperty)!;
        set => SetProperty(NoteProperty, value);
    }

    public override string EntityType => "RuleProbe";

    public override string EntityKey => Id.ToString();

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(NameProperty));
        BusinessRules.AddRule(new MaxLength(NameProperty, 50));
    }

    [Create]
    private void Create()
    {
        LoadProperty(IdProperty, Guid.CreateVersion7());
        BusinessRules.CheckRules();
    }
}
