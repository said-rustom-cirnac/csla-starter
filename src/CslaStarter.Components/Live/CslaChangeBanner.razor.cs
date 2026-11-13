using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Live;

/// <summary>
/// Shown on a form with unsaved edits when someone else saved the record: "Jane saved changes. Reload / Keep mine". If
/// the user keeps theirs and saves, the save fails the concurrency check and the conflict dialog opens.
/// </summary>
public partial class CslaChangeBanner
{
    /// <summary>Who saved; empty when unknown (changes found after a reconnect).</summary>
    [Parameter]
    public string? ChangedBy { get; set; }

    /// <summary>The record was deleted: there's nothing to reload, and the form can't save.</summary>
    [Parameter]
    public bool Deleted { get; set; }

    [Parameter]
    public EventCallback OnReload { get; set; }

    [Parameter]
    public EventCallback OnKeepMine { get; set; }

    private string Text =>
        Deleted ? Messages.Live_Deleted
        : string.IsNullOrWhiteSpace(ChangedBy) ? Messages.Live_ChangedElsewhere
        : Messages.Live_ChangedBy.Replace("{0}", ChangedBy, StringComparison.Ordinal);
}
