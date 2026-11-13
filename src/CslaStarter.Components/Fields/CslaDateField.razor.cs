using CslaStarter.Components.Interop;

namespace CslaStarter.Components.Fields;

/// <summary>A date field (<c>gcds-date-input</c>, full format, yyyy-MM-dd). Read-only renders as text.</summary>
public partial class CslaDateField
{
    private void OnChange(GcdsEventArgs e) => SetText(e.Value);
}
