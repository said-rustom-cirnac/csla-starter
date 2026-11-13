using Csla;
using CslaStarter.Core;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;

namespace CslaStarter.Business.Base;

/// <summary>
/// Base of every editable business object: audit stamps (read-only, loaded from the database), an identity for
/// change notifications and history, and rule lookups the components use for required markers and length limits.
/// </summary>
[Serializable]
public abstract class AppBusinessBase<T> : BusinessBase<T>, IEntityIdentity, IRuleHints
    where T : AppBusinessBase<T>
{
    public static readonly PropertyInfo<DateTimeOffset> CreatedOnProperty = RegisterProperty<DateTimeOffset>(nameof(CreatedOn));
    public static readonly PropertyInfo<string> CreatedByProperty = RegisterProperty<string>(nameof(CreatedBy));
    public static readonly PropertyInfo<DateTimeOffset> ModifiedOnProperty = RegisterProperty<DateTimeOffset>(nameof(ModifiedOn));
    public static readonly PropertyInfo<string> ModifiedByProperty = RegisterProperty<string>(nameof(ModifiedBy));

    [LocalizedDisplayName(nameof(Labels.Audit_CreatedOn))]
    public DateTimeOffset CreatedOn => GetProperty(CreatedOnProperty);

    [LocalizedDisplayName(nameof(Labels.Audit_CreatedBy))]
    public string CreatedBy => GetProperty(CreatedByProperty)!;

    [LocalizedDisplayName(nameof(Labels.Audit_ModifiedOn))]
    public DateTimeOffset ModifiedOn => GetProperty(ModifiedOnProperty);

    [LocalizedDisplayName(nameof(Labels.Audit_ModifiedBy))]
    public string ModifiedBy => GetProperty(ModifiedByProperty)!;

    /// <summary>The name changes and history are recorded under; it matches the data layer's [AuditEntity] name.</summary>
    public abstract string EntityType { get; }

    public abstract string EntityKey { get; }

    public string[] GetRuleDescriptions() => BusinessRules.GetRuleDescriptions();

    /// <summary>True when <paramref name="propertyName"/> has a rule of that type, e.g. "Required".</summary>
    public bool PropertyHasRule(string propertyName, string ruleTypeName) =>
        RuleInspector.Has(GetType(), GetRuleDescriptions, propertyName, ruleTypeName);

    /// <summary>The MaxLength rule's limit for <paramref name="propertyName"/>, or null when it has none.</summary>
    public int? MaxLengthOf(string propertyName) => RuleInspector.MaxLength(GetType(), GetRuleDescriptions, propertyName);

    protected void LoadAudit(DateTimeOffset createdOn, string createdBy, DateTimeOffset modifiedOn, string modifiedBy)
    {
        LoadProperty(CreatedOnProperty, createdOn);
        LoadProperty(CreatedByProperty, createdBy);
        LoadProperty(ModifiedOnProperty, modifiedOn);
        LoadProperty(ModifiedByProperty, modifiedBy);
    }
}
