using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Dialogs;

/// <summary>Asks before something that can't be undone (delete, leave with unsaved changes).</summary>
public partial class CslaConfirmDialog
{
    private const string MessageId = "csla-confirm-message";

    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string ConfirmText { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string CancelText { get; set; } = string.Empty;

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }
}
