using System.Linq.Expressions;
using CslaStarter.Components.Base;
using CslaStarter.Components.Forms;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CslaStarter.Components.Fields;

/// <summary>
/// A field bound to one CSLA property with <c>For="() => form.Model.Prop"</c>. Label, errors, warnings, required,
/// length limit, read-only and visibility all come from the business object; the field never validates on its own.
/// </summary>
public abstract class CslaFieldBase<TValue> : CslaBaseComponent
{
    [Parameter, EditorRequired]
    public Expression<Func<TValue>> For { get; set; } = default!;

    /// <summary>Replaces the hint taken from warning and information messages.</summary>
    [Parameter]
    public string? Hint { get; set; }

    /// <summary>Replaces the property's friendly name.</summary>
    [Parameter]
    public string? Label { get; set; }

    protected object Model { get; private set; } = default!;

    protected string PropertyName { get; private set; } = string.Empty;

    protected Csla.Blazor.IPropertyInfo Property { get; private set; } = default!;

    protected FieldHint Info { get; private set; } = default!;

    protected string FieldId => Form!.FieldId(Model, PropertyName);

    protected string InputId => FieldId + "-input";

    protected string LabelText => Label ?? Property.FriendlyName;

    protected bool IsChanged => Form!.IsChanged(Model, PropertyName);

    protected bool IsHighlighted => Form!.IsHighlighted(Model, PropertyName);

    protected string? Text => FieldValue.ToText(Property.Value);

    [CascadingParameter]
    private ICslaFormContext? Form { get; set; }

    protected override void OnParametersSet()
    {
        if (Form is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside a CslaForm.");
        }

        var field = FieldIdentifier.Create(For);
        Model = field.Model;
        PropertyName = field.FieldName;
        Describe();
    }

    protected void SetValue(TValue? value)
    {
        Property.Value = value;
        Form!.Touch(Model, PropertyName);
        Describe();
    }

    /// <summary>Sets the value parsed from the field's text; text that doesn't parse leaves the property as it was.</summary>
    protected void SetText(string? text)
    {
        if (FieldValue.TryParse<TValue>(text, out var value))
        {
            SetValue(value);
        }
    }

    protected void OnBlur()
    {
        Form!.Touch(Model, PropertyName);
        Describe();
    }

    private void Describe()
    {
        Property = Form!.PropertyInfo(Model, PropertyName);
        Info = FieldHints.Describe(Model, Property, Hint, Form.ShowError(Model, PropertyName), Messages.Field_Checking);
    }
}
