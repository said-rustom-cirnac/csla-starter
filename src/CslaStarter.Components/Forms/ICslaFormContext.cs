namespace CslaStarter.Components.Forms;

/// <summary>What a field needs from the form around it, whatever the form's object type.</summary>
public interface ICslaFormContext
{
    /// <summary>A stable DOM id for the field bound to <paramref name="propertyName"/> of <paramref name="model"/>.</summary>
    string FieldId(object model, string propertyName);

    Csla.Blazor.IPropertyInfo PropertyInfo(object model, string propertyName);

    /// <summary>Errors show once the field was touched or a save was attempted.</summary>
    bool ShowError(object model, string propertyName);

    /// <summary>The value differs from the one loaded (root properties).</summary>
    bool IsChanged(object model, string propertyName);

    /// <summary>Someone else just changed this value (live update); the highlight fades after a few seconds.</summary>
    bool IsHighlighted(object model, string propertyName);

    void Touch(object model, string propertyName);
}
