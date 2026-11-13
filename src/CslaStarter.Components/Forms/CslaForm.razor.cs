using System.Runtime.ExceptionServices;
using CslaStarter.Components.Base;
using CslaStarter.Components.Services;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Forms;

/// <summary>
/// Edits one business object: loads it through <see cref="Load"/>, cascades a <see cref="CslaFormContext{T}"/> to its
/// fields, and saves or cancels (undo) through an <see cref="AppViewModel{T}"/>. Errors show once a field is touched or
/// a save is attempted; CSLA is the only validator (the form is <c>novalidate</c>).
/// </summary>
public partial class CslaForm<T> : CslaBaseComponent<T>
    where T : class
{
    [Parameter, EditorRequired]
    public Func<Task<T>> Load { get; set; } = default!;

    [Parameter]
    public RenderFragment<CslaFormContext<T>>? ChildContent { get; set; }

    /// <summary>Raised after a successful save, with the saved object (CSLA returns a new instance).</summary>
    [Parameter]
    public EventCallback<T> Saved { get; set; }

    public AppViewModel<T> ViewModel { get; private set; } = default!;

    public bool IsDirty => ViewModel?.IsDirty == true;

    protected CslaFormContext<T>? FormContext { get; private set; }

    protected override T? Current => ViewModel?.Model;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Saves when the object is savable; otherwise shows every error. Returns whether it saved.</summary>
    public async Task<bool> SaveAsync()
    {
        if (FormContext is null)
        {
            return false;
        }

        FormContext.Submitted = true;
        if (!ViewModel.IsSavable)
        {
            StateHasChanged();
            return false;
        }

        var saved = await RunAsync("save", async cancellationToken =>
        {
            var before = ViewModel.Exception;
            await ViewModel.SaveAsync(cancellationToken);
            ThrowIfFailed(before);
        });
        if (saved)
        {
            FormContext.Snapshot();
            Toasts.Show(ToastKind.Success, Messages.Toast_Saved);
            Announcer.Announce(Messages.Toast_Saved);
            await Saved.InvokeAsync(ViewModel.Model!);
        }

        return saved;
    }

    /// <summary>Undoes every change since the object was loaded or last saved.</summary>
    public async Task CancelAsync()
    {
        await ViewModel.CancelAsync();
        FormContext?.Snapshot();
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        ViewModel = new AppViewModel<T>(ApplicationContext);
        ViewModel.ModelPropertyChanged += (_, _) => RenderLater();
        ViewModel.ModelChildChanged += (_, _) => RenderLater();
        ViewModel.BusyChanged += RenderLater;
        await RunAsync("load", async _ =>
        {
            var before = ViewModel.Exception;
            await ViewModel.RefreshAsync(async () => await Load());
            ThrowIfFailed(before);
            FormContext = new CslaFormContext<T>(ComponentId, ViewModel, Time);
        });
    }

    protected async Task SubmitAsync() => await SaveAsync();

    // CSLA's view model keeps the exception of a failed load or save instead of throwing; raise it so the base
    // component classifies and shows it.
    private void ThrowIfFailed(Exception? before)
    {
        if (ViewModel.Exception is { } failure && !ReferenceEquals(failure, before))
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    // Rule results and busy changes can arrive off the renderer (async rules finish on the thread pool).
    private void RenderLater() => _ = InvokeAsync(StateHasChanged);
}
