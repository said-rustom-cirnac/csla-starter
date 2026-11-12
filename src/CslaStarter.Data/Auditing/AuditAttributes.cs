namespace CslaStarter.Data.Auditing;

/// <summary>The name an entity's changes are recorded under; it matches the business object's entity type.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditEntityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Records that the property changed, never its values (sensitive data).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditMaskAttribute : Attribute;

/// <summary>Never audited: on a class, the whole entity; on a property, that column.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute;

/// <summary>The property holds codes of a *Type lookup, so the history can show their descriptions.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditTypeCodeAttribute(string typeName) : Attribute
{
    public string TypeName { get; } = typeName;
}

/// <summary>A child row whose history belongs to its aggregate root.</summary>
public interface IHasAuditRoot
{
    string AuditRootType { get; }

    string AuditRootKey { get; }
}
