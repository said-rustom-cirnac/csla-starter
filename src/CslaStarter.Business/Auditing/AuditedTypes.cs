using System.Collections.Frozen;
using System.Reflection;

namespace CslaStarter.Business.Auditing;

/// <summary>The business types marked <see cref="HistoryOfAttribute"/>, by the entity type whose history they guard.</summary>
public static class AuditedTypes
{
    private static readonly Lazy<FrozenDictionary<string, Type>> ByEntityType = new(Scan);

    public static IReadOnlyCollection<string> EntityTypes => ByEntityType.Value.Keys;

    public static Type? Find(string entityType) => ByEntityType.Value.GetValueOrDefault(entityType);

    private static FrozenDictionary<string, Type> Scan()
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in typeof(AuditedTypes).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<HistoryOfAttribute>() is { } history && !types.TryAdd(history.EntityType, type))
            {
                throw new InvalidOperationException($"Both {types[history.EntityType].Name} and {type.Name} claim the history of {history.EntityType}.");
            }
        }

        return types.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
