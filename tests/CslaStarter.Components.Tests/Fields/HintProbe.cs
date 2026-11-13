using Csla;
using Csla.Core;
using Csla.Rules;
using Csla.Rules.CommonRules;
using CslaStarter.Business.Base;

namespace CslaStarter.Components.Tests.Fields;

/// <summary>A business object with warning and information rules and a code list, for field tests.</summary>
[Serializable]
public sealed class HintProbe : AppBusinessBase<HintProbe>
{
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public static readonly PropertyInfo<string> NoteProperty = RegisterProperty<string>(nameof(Note));
    public static readonly PropertyInfo<List<int>> TagsProperty = RegisterProperty<List<int>>(nameof(Tags));
    public static readonly PropertyInfo<bool> ActiveProperty = RegisterProperty<bool>(nameof(Active));

    public string Name
    {
        get => GetProperty(NameProperty)!;
        set => SetProperty(NameProperty, value);
    }

    public string Note
    {
        get => GetProperty(NoteProperty)!;
        set => SetProperty(NoteProperty, value);
    }

    public List<int> Tags
    {
        get => GetProperty(TagsProperty)!;
        set => SetProperty(TagsProperty, value);
    }

    public bool Active
    {
        get => GetProperty(ActiveProperty);
        set => SetProperty(ActiveProperty, value);
    }

    public override string EntityType => "HintProbe";

    public override string EntityKey => "probe-1";

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(NameProperty));
        BusinessRules.AddRule(new Advice(NoteProperty, "Short notes read better.", RuleSeverity.Warning));
        BusinessRules.AddRule(new Advice(NoteProperty, "Notes are visible to the team.", RuleSeverity.Information));
    }

    [Create]
    private void Create()
    {
        using (BypassPropertyChecks)
        {
            Name = string.Empty;
            Note = "A note";
            Tags = [];
        }

        BusinessRules.CheckRules();
    }

    private sealed class Advice : BusinessRule
    {
        private readonly string _text;
        private readonly RuleSeverity _severity;

        public Advice(IPropertyInfo property, string text, RuleSeverity severity)
            : base(property)
        {
            _text = text;
            _severity = severity;
            RuleUri.AddQueryParameter("severity", severity.ToString());
        }

        protected override void Execute(IRuleContext context)
        {
            if (_severity == RuleSeverity.Warning)
            {
                context.AddWarningResult(_text);
            }
            else
            {
                context.AddInformationResult(_text);
            }
        }
    }
}
