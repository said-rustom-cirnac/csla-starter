using CslaStarter.Business.Base;

namespace CslaStarter.Components.Forms;

/// <summary>Everything a field shows that comes from the business object rather than from markup.</summary>
public sealed record FieldHint(bool Visible, bool ReadOnly, bool Required, int? MaxLength, bool Checking, string? Hint, string? Error);

/// <summary>Reads a property's permissions, rules and broken-rule texts into a <see cref="FieldHint"/>.</summary>
public static class FieldHints
{
    public static FieldHint Describe(object model, Csla.Blazor.IPropertyInfo property, string? hint, bool showError, string checkingText)
    {
        ArgumentNullException.ThrowIfNull(property);
        var rules = model as IRuleHints;
        var readOnly = !property.CanWrite;
        var checking = property.IsBusy;
        var advice = string.Join(' ', new[] { property.WarningText, property.InformationText }.Where(t => !string.IsNullOrWhiteSpace(t)));
        return new FieldHint(
            Visible: property.CanRead,
            ReadOnly: readOnly,

            // GCDS forces disabled=false when required is set, so a field the user can't change is never "required".
            Required: !readOnly && rules?.PropertyHasRule(property.PropertyName, "Required") == true,
            MaxLength: rules?.MaxLengthOf(property.PropertyName),
            Checking: checking,
            Hint: checking ? checkingText : hint ?? (advice.Length > 0 ? advice : null),
            Error: showError && !string.IsNullOrWhiteSpace(property.ErrorText) ? property.ErrorText : null);
    }
}
