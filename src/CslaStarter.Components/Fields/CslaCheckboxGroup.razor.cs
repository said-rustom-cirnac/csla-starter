using CslaStarter.Components.Interop;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>Several checkboxes (<c>gcds-checkboxes</c>) bound to a <c>List&lt;TValue&gt;</c> of the checked values.</summary>
public partial class CslaCheckboxGroup<TValue>
{
    [Parameter, EditorRequired]
    public IReadOnlyList<KeyValuePair<TValue, string>> Options { get; set; } = [];

    private string OptionsJson => ChoiceOptions.Serialize(FieldId, Options, value => (Property.Value as List<TValue>)?.Contains(value) == true);

    private void OnChange(GcdsEventArgs e)
    {
        var checkedValues = new List<TValue>();
        foreach (var text in (e.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (FieldValue.TryParse<TValue>(text, out var value) && value is not null)
            {
                checkedValues.Add(value);
            }
        }

        SetValue(checkedValues);
    }
}
