using System.Globalization;

namespace CslaStarter.Components.Fields;

/// <summary>
/// Values to and from the text GC Design System fields carry. Always invariant: browsers send numbers with a dot and
/// dates as yyyy-MM-dd whatever the page language.
/// </summary>
public static class FieldValue
{
    public static string? ToText(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>False when <paramref name="text"/> isn't a valid <typeparamref name="T"/>; the property then keeps its value.</summary>
    public static bool TryParse<T>(string? text, out T? value)
    {
        value = default;
        var nullable = Nullable.GetUnderlyingType(typeof(T));
        var type = nullable ?? typeof(T);
        if (type == typeof(string))
        {
            value = (T)(object)(text ?? string.Empty);
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            if (type == typeof(bool))
            {
                value = (T)(object)false;
                return true;
            }

            return nullable is not null || !typeof(T).IsValueType;
        }

        object? parsed = type switch
        {
            _ when type == typeof(int) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            _ when type == typeof(long) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null,
            _ when type == typeof(decimal) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : null,
            _ when type == typeof(double) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ when type == typeof(bool) => bool.TryParse(text, out var b) ? b : null,
            _ when type == typeof(Guid) => Guid.TryParse(text, out var g) ? g : null,
            _ when type == typeof(DateOnly) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
            _ when type.IsEnum => Enum.TryParse(type, text, ignoreCase: true, out var e) ? e : null,
            _ => null,
        };

        if (parsed is null)
        {
            return false;
        }

        value = (T)parsed;
        return true;
    }
}
