using CslaStarter.Components.Interop;

namespace CslaStarter.Components.Fields;

/// <summary>A multi-line text field (<c>gcds-textarea</c>); its character count is the MaxLength rule. Read-only renders as text.</summary>
public partial class CslaTextArea
{
    private void OnInput(GcdsEventArgs e) => SetText(e.Value);
}
