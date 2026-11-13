using System.Globalization;
using CslaStarter.Components.Drafts;
using CslaStarter.Core.Resources;
using CslaStarter.Core.Sessions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CslaStarter.Components.Session;

/// <summary>
/// The session-timeout warning. Real user input (keyboard, pointer, reported by session.js) resets the clock. At the
/// warning it asks "Stay signed in?" and tells screen readers; when the session expires it writes every open form's
/// draft, then signs out (a POST with its antiforgery token). Place one in the layout.
/// </summary>
public partial class CslaSessionTimeout
{
    private ElementReference _signOutForm;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CslaSessionTimeout>? _self;
    private bool _signingOut;

    protected bool Warning { get; private set; }

    protected string MessageId => ComponentId + "-message";

    protected string WarningText =>
        Messages.Session_Warning.Replace("{0}", Math.Max(1, (int)Math.Ceiling(Session.Remaining.TotalMinutes)).ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

    [Inject]
    private ISessionActivity Session { get; set; } = default!;

    [Inject]
    private DraftFlushRegistry Drafts { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [JSInvokable]
    public void ReportActivity() => Session.RecordActivity();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Session.PhaseChanged += OnPhaseChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/CslaStarter.Components/session.js");
            _self = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("watchActivity", _self);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Session.PhaseChanged -= OnPhaseChanged;
        _self?.Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; nothing to release in the browser.
            }
        }

        await base.DisposeAsyncCore();
    }

    protected void StaySignedIn()
    {
        Session.RecordActivity();
        Warning = false;
    }

    private void OnPhaseChanged(SessionPhase phase) => _ = InvokeAsync(async () =>
    {
        if (phase == SessionPhase.Warning)
        {
            Warning = true;
            Announcer.Announce(WarningText, assertive: true);
        }
        else if (phase == SessionPhase.Expired)
        {
            await SignOutAsync();
        }

        StateHasChanged();
    });

    private async Task SignOutAsync()
    {
        if (_signingOut)
        {
            return;
        }

        _signingOut = true;
        Warning = false;
        await Drafts.FlushAllAsync();
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("submit", _signOutForm);
        }
    }
}
