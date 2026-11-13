using CslaStarter.Components.Interop;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>A number field (<c>gcds-input type="number"</c>) for int, long, decimal or double, nullable or not.</summary>
public partial class CslaNumberField<TValue>
{
    /// <summary>The input's step; "any" allows decimals.</summary>
    [Parameter]
    public string Step { get; set; } = "any";

    private void OnInput(GcdsEventArgs e) => SetText(e.Value);
}
