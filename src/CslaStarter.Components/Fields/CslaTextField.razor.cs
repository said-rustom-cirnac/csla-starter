using CslaStarter.Components.Interop;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>A one-line text field (<c>gcds-input</c>).</summary>
public partial class CslaTextField
{
    /// <summary>text (default), email, tel, url or search.</summary>
    [Parameter]
    public string Type { get; set; } = "text";

    [Parameter]
    public string? Autocomplete { get; set; }

    private void OnInput(GcdsEventArgs e) => SetText(e.Value);
}
