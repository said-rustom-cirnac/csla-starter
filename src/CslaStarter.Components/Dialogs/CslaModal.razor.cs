using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CslaStarter.Components.Dialogs;

/// <summary>
/// A modal <c>dialog</c> (dialog.js): focus moves into it and back out when it goes, the page behind is inert, and Esc
/// calls <see cref="OnCancel"/> instead of closing it behind Blazor's back (with no <see cref="OnCancel"/>, Esc does
/// nothing). Put <c>autofocus</c> on the control that should take focus: for a destructive choice, the safe one.
/// </summary>
public partial class CslaModal
{
    private ElementReference _dialog;
    private IJSObjectReference? _module;

    /// <summary><c>alertdialog</c> for a question that needs an answer; the default is a plain dialog.</summary>
    [Parameter]
    public string? Role { get; set; }

    [Parameter]
    public string? LabelledBy { get; set; }

    [Parameter]
    public string? DescribedBy { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !IsDisposed)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/CslaStarter.Components/dialog.js");
            await _module.InvokeVoidAsync("showModal", _dialog);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("close", _dialog);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone with the browser.
            }
        }

        await base.DisposeAsyncCore();
    }
}
