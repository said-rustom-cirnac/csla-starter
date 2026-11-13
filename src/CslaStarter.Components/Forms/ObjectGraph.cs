using System.Collections;
using Csla.Core;
using CslaStarter.Core;

namespace CslaStarter.Components.Forms;

/// <summary>
/// Reads and writes a CSLA object's registered properties generically, for diffs and merges: the values a user edits
/// (simple types with a public setter) and the child lists (items identified by <see cref="IEntityIdentity"/>). Access
/// goes through the public properties, so permissions apply: a hidden value reads as its default on every copy (and so
/// never differs), and only values the user may write were ever changed by them.
/// </summary>
internal static class ObjectGraph
{
    public static IEnumerable<IPropertyInfo> Values(object model) =>
        Registered(model).Where(p => IsSimple(p.Type) && model.GetType().GetProperty(p.Name)?.SetMethod?.IsPublic == true);

    public static IEnumerable<(IPropertyInfo Property, IList Items)> ChildLists(object model)
    {
        foreach (var property in Registered(model).Where(p => typeof(IList).IsAssignableFrom(p.Type)))
        {
            if (Read(model, property) is IList items && items.Cast<object>().All(item => item is IEntityIdentity))
            {
                yield return (property, items);
            }
        }
    }

    public static object? Read(object model, IPropertyInfo property) => model.GetType().GetProperty(property.Name)?.GetValue(model);

    /// <summary>Sets the value through its setter: rules run and the object becomes dirty.</summary>
    public static void Write(object model, IPropertyInfo property, object? value) => model.GetType().GetProperty(property.Name)!.SetValue(model, value);

    public static Dictionary<string, object> ByKey(IList items) =>
        items.Cast<object>().ToDictionary(item => ((IEntityIdentity)item).EntityKey, StringComparer.Ordinal);

    // In declaration order (CSLA's registry has its own), so a diff lists fields the way the class reads.
    private static IEnumerable<IPropertyInfo> Registered(object model) =>
        Csla.Core.FieldManager.PropertyInfoManager.GetRegisteredProperties(model.GetType())
            .OrderBy(p => model.GetType().GetProperty(p.Name)?.MetadataToken ?? int.MaxValue);

    private static bool IsSimple(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(decimal)
            || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly) || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset) || underlying == typeof(Guid);
    }
}
