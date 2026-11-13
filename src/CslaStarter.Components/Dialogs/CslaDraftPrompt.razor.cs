using System.Globalization;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Dialogs;

/// <summary>
/// "Restore your unsaved changes from 10:42?", with a warning when the record changed since the draft was taken. An
/// inline region above the form, not a modal: screen readers hear the question when it appears.
/// </summary>
public partial class CslaDraftPrompt
{
    [Parameter, EditorRequired]
    public DateTimeOffset SavedAt { get; set; }

    [Parameter]
    public bool RecordChanged { get; set; }

    [Parameter]
    public EventCallback OnRestore { get; set; }

    [Parameter]
    public EventCallback OnDiscard { get; set; }

    private string TitleId => ComponentId + "-title";

    private string Prompt => Messages.Draft_RestorePrompt.Replace("{0}", SavedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture), StringComparison.Ordinal);

    protected override void OnInitialized() => Announcer.Announce(Prompt);
}
