namespace CslaStarter.Core.Realtime;

/// <summary>What a subscriber listens to: one record (an open editor) or every record of a type (a list).</summary>
public readonly record struct ChangeTopic
{
    private ChangeTopic(string entityType, string? entityKey)
    {
        EntityType = entityType;
        EntityKey = entityKey;
    }

    public string EntityType { get; }

    /// <summary>Null for a type topic.</summary>
    public string? EntityKey { get; }

    public bool IsDefault => EntityType is null;

    public static ChangeTopic ForRecord(string entityType, string entityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityKey);
        return new(entityType, entityKey);
    }

    public static ChangeTopic ForType(string entityType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        return new(entityType, null);
    }

    public override string ToString() => EntityKey is null ? EntityType : $"{EntityType}:{EntityKey}";
}
