using System.Text.Json;
using CslaStarter.Components.Fields;
using CslaStarter.Components.Forms;
using CslaStarter.Components.Interop;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Dialogs;

/// <summary>
/// Opens when a save fails because someone else saved first. Lists every value where theirs and mine differ (root
/// first, then children). "Use theirs" discards my edits; "Apply mine" puts my changes onto their copy for review.
/// Where both of us changed a value the user can tick "Keep theirs".
/// </summary>
public partial class CslaConflictDialog
{
    private readonly HashSet<string> _keepTheirs = new(StringComparer.Ordinal);

    [Parameter, EditorRequired]
    public IReadOnlyList<FieldDifference> Differences { get; set; } = [];

    [Parameter]
    public IReadOnlyList<FieldDifference> BothChanged { get; set; } = [];

    [Parameter]
    public EventCallback OnUseTheirs { get; set; }

    /// <summary>Called with the keys of the values to keep theirs for.</summary>
    [Parameter]
    public EventCallback<IReadOnlySet<string>> OnApplyMine { get; set; }

    private string TitleId => ComponentId + "-title";

    // A sensitive value is shown only through its field's Show button, which is logged.
    private static string? Show(FieldDifference difference, object? value) =>
        difference.Property.Length == 0 ? value is true ? Messages.Conflict_Added : Messages.Conflict_Removed
        : difference.IsSensitive ? string.IsNullOrEmpty(FieldValue.ToText(value)) ? string.Empty : FieldValue.MaskText
        : FieldValue.ToText(value);

    private static string KeepTheirsOption(FieldDifference difference) =>
        JsonSerializer.Serialize(new[] { new { id = "keep-" + difference.Key, label = Messages.Conflict_KeepTheirs, value = "keep", @checked = false } });

    private bool IsBothChanged(FieldDifference difference) => BothChanged.Any(d => d.Key == difference.Key);

    private void ToggleKeepTheirs(FieldDifference difference, GcdsEventArgs e)
    {
        if (e.Value?.Contains("keep", StringComparison.Ordinal) == true)
        {
            _keepTheirs.Add(difference.Key);
        }
        else
        {
            _keepTheirs.Remove(difference.Key);
        }
    }

    private Task ApplyMineAsync() => OnApplyMine.InvokeAsync(_keepTheirs);
}
