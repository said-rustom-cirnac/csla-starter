using CslaStarter.Components.Interop;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>One choice among a few (<c>gcds-radios</c>).</summary>
public partial class CslaRadioGroup<TValue>
{
    [Parameter, EditorRequired]
    public IReadOnlyList<KeyValuePair<TValue, string>> Options { get; set; } = [];

    private string OptionsJson => ChoiceOptions.Serialize(FieldId, Options, value => Equals(value, Property.Value));

    private void OnChange(GcdsEventArgs e) => SetText(e.Value);
}
