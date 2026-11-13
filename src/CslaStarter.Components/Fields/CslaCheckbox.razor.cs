using CslaStarter.Components.Interop;

namespace CslaStarter.Components.Fields;

/// <summary>One yes/no checkbox (<c>gcds-checkboxes</c> with a single option, labelled with the property's name).</summary>
public partial class CslaCheckbox
{
    private string OptionsJson => ChoiceOptions.Serialize(FieldId, [new KeyValuePair<bool, string>(true, LabelText)], _ => Property.Value is true);

    private void OnChange(GcdsEventArgs e) => SetValue(e.Value?.Split(',').Contains("true") == true);
}
