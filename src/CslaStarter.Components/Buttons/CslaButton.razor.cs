using CslaStarter.Components.Forms;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Buttons;

public enum ButtonKind
{
    /// <summary>Saves the form; enabled only when the object is savable and nothing is running.</summary>
    Save,

    /// <summary>Undoes the form's edits; enabled while there are some.</summary>
    Cancel,

    /// <summary>Deletes the object after a confirmation; shown only to users who may delete it.</summary>
    Delete,

    /// <summary>Starts a new object (<see cref="CslaButton.OnClick"/>); shown only to users who may create one.</summary>
    New,

    /// <summary>Anything else (<see cref="CslaButton.OnClick"/>); disabled while it or the form is busy.</summary>
    Custom,
}

/// <summary>A form button (<c>gcds-button</c>) that follows the object's permissions, savable and busy state.</summary>
public partial class CslaButton
{
    [Parameter]
    public ButtonKind Kind { get; set; } = ButtonKind.Custom;

    /// <summary>Replaces the default text (Save, Cancel, Delete, New).</summary>
    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    protected bool Confirming { get; set; }

    [CascadingParameter]
    private CslaFormState? Form { get; set; }

    private bool Visible => Kind switch
    {
        ButtonKind.Delete => Form?.CanDelete == true,
        ButtonKind.New => Form is null || Form.CanCreate,
        _ => true,
    };

    private bool Disabled => Kind switch
    {
        ButtonKind.Save => Form is not { CanSave: true },
        ButtonKind.Cancel => Form is not { IsDirty: true, IsBusy: false },
        _ => IsBusy || Form?.IsBusy == true,
    };

    private string Role => Kind switch
    {
        ButtonKind.Save => "primary",
        ButtonKind.Delete => "danger",
        _ => "secondary",
    };

    private string Text => Label ?? Kind switch
    {
        ButtonKind.Save => Labels.Common_Save,
        ButtonKind.Cancel => Labels.Common_Cancel,
        ButtonKind.Delete => Labels.Common_Delete,
        ButtonKind.New => Labels.Common_New,
        _ => string.Empty,
    };

    protected override void OnParametersSet()
    {
        if (Kind is ButtonKind.Save or ButtonKind.Cancel or ButtonKind.Delete && Form is null)
        {
            throw new InvalidOperationException($"A {Kind} button must be inside a CslaForm.");
        }
    }

    private async Task ClickAsync()
    {
        if (Disabled)
        {
            return;
        }

        switch (Kind)
        {
            case ButtonKind.Save:
                await Form!.Actions.SaveAsync();
                break;
            case ButtonKind.Cancel:
                await Form!.Actions.CancelAsync();
                break;
            case ButtonKind.Delete:
                Confirming = true;
                break;
            default:
                // Through the base component: a failure is shown like any other, and the circuit lives on.
                await RunAsync("click", _ => OnClick.InvokeAsync());
                break;
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        Confirming = false;
        await Form!.Actions.DeleteAsync();
    }
}
