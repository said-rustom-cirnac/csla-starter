using System.Text.Json;

namespace CslaStarter.Components.Fields;

/// <summary>The JSON <c>options</c> string gcds-radios and gcds-checkboxes take (object props are passed as JSON).</summary>
internal static class ChoiceOptions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize<TValue>(string fieldId, IEnumerable<KeyValuePair<TValue, string>> options, Func<TValue, bool> isChecked) =>
        JsonSerializer.Serialize(
            options.Select((option, index) => new Choice($"{fieldId}-{index}", option.Value, FieldValue.ToText(option.Key) ?? string.Empty, isChecked(option.Key))),
            Json);

    private sealed record Choice(string Id, string Label, string Value, bool Checked);
}
