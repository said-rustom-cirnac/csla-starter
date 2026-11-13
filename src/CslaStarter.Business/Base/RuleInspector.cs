using System.Collections.Concurrent;
using System.Globalization;
using Csla.Rules;

namespace CslaStarter.Business.Base;

/// <summary>
/// Answers "does this property have a Required / MaxLength rule?" from CSLA's rule descriptions
/// (rule://csla.rules.commonrules.maxlength/Name?max=50), so components never repeat rules in markup.
/// Rules are registered once per type, so answers are cached per type and property.
/// </summary>
internal static class RuleInspector
{
    private static readonly ConcurrentDictionary<(Type, string, string), RuleUri?> Cache = new();

    public static bool Has(Type type, Func<string[]> descriptions, string propertyName, string ruleTypeName) =>
        Find(type, descriptions, propertyName, ruleTypeName) is not null;

    public static int? MaxLength(Type type, Func<string[]> descriptions, string propertyName) =>
        Find(type, descriptions, propertyName, "MaxLength") is { } uri
        && uri.Arguments is { } arguments
        && arguments.TryGetValue("max", out var max)
        && int.TryParse(max, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            ? length
            : null;

    private static RuleUri? Find(Type type, Func<string[]> descriptions, string propertyName, string ruleTypeName) =>
        Cache.GetOrAdd((type, propertyName, ruleTypeName), _ => descriptions()
            .Select(d => new RuleUri(d))
            .FirstOrDefault(uri =>
                string.Equals(uri.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase)
                && uri.RuleTypeName?.EndsWith("." + ruleTypeName, StringComparison.OrdinalIgnoreCase) == true));
}
