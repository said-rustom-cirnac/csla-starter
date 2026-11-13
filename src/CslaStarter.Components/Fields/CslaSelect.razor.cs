using CslaStarter.Components.Interop;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>
/// A dropdown (<c>gcds-select</c>). For a *Type, pass <c>TypeOptions.From(list, current)</c>: active values plus the
/// record's current one, in the current language. Read-only renders the selected text.
/// </summary>
public partial class CslaSelect<TValue>
{
    [Parameter, EditorRequired]
    public IReadOnlyList<KeyValuePair<TValue, string>> Options { get; set; } = [];

    /// <summary>The empty first choice ("Select an option").</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    private string? SelectedText => Options.FirstOrDefault(o => Equals(o.Key, Property.Value)).Value;

    private void OnChange(GcdsEventArgs e) => SetText(e.Value);
}
