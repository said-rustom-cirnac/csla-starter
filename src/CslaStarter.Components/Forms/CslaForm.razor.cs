using System.Runtime.ExceptionServices;
using Csla.Core;
using CslaStarter.Business.Base;
using CslaStarter.Business.Drafts;
using CslaStarter.Business.Errors;
using CslaStarter.Components.Base;
using CslaStarter.Components.Drafts;
using CslaStarter.Components.Services;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CslaStarter.Components.Forms;

/// <summary>
/// Edits one business object: loads it through <see cref="Load"/>, cascades a <see cref="CslaFormContext{T}"/> to its
/// fields, and saves, cancels and deletes through an <see cref="AppViewModel{T}"/>. It keeps the record live:
/// <list type="bullet">
/// <item>someone else's save refreshes a clean editor (changed fields highlighted, "Updated by Jane" announced) and
/// shows a Reload / Keep mine banner on a dirty one;</item>
/// <item>after a backplane reconnect it re-checks the version;</item>
/// <item>if the user loses access, it closes with a message and discards the draft;</item>
/// <item>a save that loses the race opens the conflict dialog;</item>
/// <item>presence says who else has the record open;</item>
/// <item>while dirty, a draft is saved 2 seconds after the last edit, and offered back when the record is opened
/// again (merged onto the record as it is now).</item>
/// </list>
/// Saves, deletes and live updates run one at a time. Every model replacement goes through a copy flattened to edit
/// level 0, so CSLA's undo always starts from what the user sees. CSLA is the only validator (the form is
/// <c>novalidate</c>).
/// </summary>
public partial class CslaForm<T> : CslaBaseComponent<T>, IDraftFlusher, ICslaFormActions
    where T : class
{
    private const string NewRecordKey = "new";

    private static readonly TimeSpan DraftAfter = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _exclusive = new(1, 1);
    private T? _original;
    private DraftKey? _draftKey;
    private ElementReference _formElement;
    private IJSObjectReference? _shortcuts;
    private DotNetObjectReference<CslaForm<T>>? _self;
    private TaskCompletionSource? _rendered;
    private bool _leaving;
    private DraftAutosave? _autosave;
    private IDisposable? _flushRegistration;
    private IDisposable? _presence;
    private PresenceMode? _presenceMode;

    [Parameter, EditorRequired]
    public Func<Task<T>> Load { get; set; } = default!;

    [Parameter]
    public RenderFragment<CslaFormContext<T>>? ChildContent { get; set; }

    /// <summary>Raised after a successful save, with the saved object (CSLA returns a new instance).</summary>
    [Parameter]
    public EventCallback<T> Saved { get; set; }

    /// <summary>Raised when the form closed itself because the user lost access to the record.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Raised after the object was deleted.</summary>
    [Parameter]
    public EventCallback Deleted { get; set; }

    /// <summary>Fields get a history button showing the changes to their value.</summary>
    [Parameter]
    public bool FieldHistory { get; set; }

    public AppViewModel<T> ViewModel { get; private set; } = default!;

    public bool IsDirty => ViewModel?.IsDirty == true;

    public bool IsClosed { get; private set; }

    protected CslaFormContext<T>? FormContext { get; private set; }

    protected override T? Current => ViewModel?.Model;

    /// <summary>Someone else's change, waiting for the user to reload or keep their edits.</summary>
    protected EntityChange? RemoteChange { get; private set; }

    protected ConflictState? Conflict { get; private set; }

    /// <summary>A draft found when the form opened, waiting for Restore or Discard.</summary>
    protected Draft? DraftOffer { get; private set; }

    protected bool DraftRecordChanged => DraftOffer is not null && ViewModel.Model is IVersioned { Version: var version } && DraftOffer.Version != version;

    /// <summary>Where the user tried to go with unsaved changes, waiting for Leave or Stay.</summary>
    protected string? PendingNavigation { get; set; }

    /// <summary>Esc with unsaved changes, waiting for Discard or Stay.</summary>
    protected bool ConfirmingCancel { get; set; }

    /// <summary>Unsaved edits that would be lost: internal navigation asks first, the browser warns on unload.</summary>
    protected bool GuardsNavigation => !_leaving && !IsClosed && ViewModel?.IsDirty == true;

    /// <summary>The field whose history is open, if any.</summary>
    protected (string EntityType, string EntityKey, string Property)? History { get; private set; }

    /// <summary>
    /// Save is offered whenever there are edits the user may save and no rule is still running; an invalid object then
    /// shows its errors.
    /// </summary>
    protected CslaFormState State => new(
        IsBusy,
        ViewModel.IsDirty,
        ViewModel.IsDirty && !IsBusy && ViewModel.Model is not INotifyBusy { IsBusy: true } && RemoteChange?.Kind != ChangeKind.Deleted
            && (ViewModel.Model is ITrackStatus { IsNew: true } ? ViewModel.CanCreate : ViewModel.CanEdit),
        ViewModel.CanDelete && ViewModel.Model is ITrackStatus { IsNew: false },
        ViewModel.CanCreate,
        this);

    protected ChangeTopic? Topic => EntityType is { Length: > 0 } type && EntityKey is { Length: > 0 } key ? ChangeTopic.ForRecord(type, key) : null;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private IPresenceTracker Presence { get; set; } = default!;

    [Inject]
    private IDraftStore Drafts { get; set; } = default!;

    [Inject]
    private DraftFlushRegistry FlushRegistry { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Ctrl+S (from shortcuts.js).</summary>
    [JSInvokable]
    public Task ShortcutSave() => SaveAsync();

    /// <summary>Esc (from shortcuts.js; ignored while a dialog is open): asks before discarding unsaved changes.</summary>
    [JSInvokable]
    public Task ShortcutCancel()
    {
        if (ViewModel?.IsDirty == true)
        {
            ConfirmingCancel = true;
            StateHasChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>Saves when the object is savable; otherwise shows every error. Returns whether it saved.</summary>
    public async Task<bool> SaveAsync()
    {
        if (FormContext is null || IsClosed || RemoteChange?.Kind == ChangeKind.Deleted)
        {
            return false;
        }

        FormContext.Submitted = true;
        if (!ViewModel.IsSavable)
        {
            FormContext.FailedSaves++;
            StateHasChanged();
            return false;
        }

        var saved = await ExclusiveAsync(() => RunAsync("save", async cancellationToken =>
        {
            var before = ViewModel.Exception;
            await ViewModel.SaveAsync(cancellationToken);
            ThrowIfFailed(before);
        }));
        if (saved)
        {
            _autosave?.Cancel();
            await DeleteDraftAsync();
            _draftKey = CurrentDraftKey();
            Baseline();
            RemoteChange = null;
            Toasts.Show(ToastKind.Success, Messages.Toast_Saved);
            Announcer.Announce(Messages.Toast_Saved);
            UpdatePresence();
            await Saved.InvokeAsync(ViewModel.Model!);
        }

        return saved;
    }

    /// <summary>Deletes the object (CslaButton Kind="Delete" asks first). A failed delete leaves the object as it was.</summary>
    public async Task<bool> DeleteAsync()
    {
        if (ViewModel.Model is not Csla.Core.BusinessBase business || !ViewModel.CanDelete)
        {
            return false;
        }

        var before = Flatten(ViewModel.Model);
        var deleted = await ExclusiveAsync(() => RunAsync("delete", async cancellationToken =>
        {
            var failedBefore = ViewModel.Exception;
            business.Delete();
            await ViewModel.SaveAsync(cancellationToken);
            ThrowIfFailed(failedBefore);
        }));
        if (deleted)
        {
            _autosave?.Cancel();
            await DeleteDraftAsync();
            IsClosed = true;
            _presence?.Dispose();
            StateHasChanged();
            await Deleted.InvokeAsync();
        }
        else
        {
            await ReplaceModelAsync(before);
        }

        return deleted;
    }

    /// <summary>Puts back the object as it was loaded or last saved, and deletes the draft.</summary>
    public async Task CancelAsync()
    {
        ConfirmingCancel = false;
        _autosave?.Cancel();
        if (_original is not null)
        {
            await ReplaceModelAsync(Flatten(_original));
        }

        await DeleteDraftAsync();
        FormContext?.Snapshot();
        UpdatePresence();
        StateHasChanged();
    }

    /// <summary>
    /// Writes the draft now when there are unsaved edits, before a language switch or a sign-out reloads the page. The
    /// form then lets the page go without asking (the draft keeps the edits): it re-renders, so the browser's
    /// leave-page warning is off, before this returns.
    /// </summary>
    public async Task FlushDraftAsync()
    {
        _autosave?.Cancel();
        await SaveDraftAsync();
        _leaving = true;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _rendered = rendered;
        StateHasChanged();
        await rendered.Task;
    }

    protected override async Task OnInitializedAsync()
    {
        ViewModel = new AppViewModel<T>(ApplicationContext);
        ViewModel.ModelPropertyChanged += (_, _) => OnModelChanged();
        ViewModel.ModelChildChanged += (_, _) => OnModelChanged();
        ViewModel.BusyChanged += RenderLater;
        var loaded = await RunAsync("load", async _ =>
        {
            var before = ViewModel.Exception;
            await ViewModel.RefreshAsync(async () => await Load());
            ThrowIfFailed(before);
        });
        if (!loaded || IsDisposed)
        {
            return;
        }

        // Listen before the fields first render, so no change made after the load can slip past.
        StartLiveUpdates();
        FormContext = new CslaFormContext<T>(ComponentId, ViewModel, Time, FieldHistory ? OpenHistory : null);
        Baseline();
        _draftKey = CurrentDraftKey();
        _autosave = new DraftAutosave(Time, DraftAfter, () => InvokeAsync(SaveDraftAsync));
        _flushRegistration = FlushRegistry.Register(this);
        await OfferDraftAsync();
    }

    protected override async Task OnErrorAsync(string operation, BusinessError failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Kind == BusinessErrorKind.Conflict && operation == "save" && await OpenConflictAsync())
        {
            Announcer.Announce(Messages.Error_Conflict, assertive: true);
            return;
        }

        await base.OnErrorAsync(operation, failure);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Interlocked.Exchange(ref _rendered, null)?.TrySetResult();
        if (firstRender && !IsDisposed)
        {
            _self = DotNetObjectReference.Create(this);
            _shortcuts = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/CslaStarter.Components/shortcuts.js");
            await _shortcuts.InvokeVoidAsync("attach", _formElement, _self);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _presence?.Dispose();
        _autosave?.Dispose();
        _flushRegistration?.Dispose();
        _rendered?.TrySetResult();
        if (_shortcuts is not null)
        {
            try
            {
                await _shortcuts.InvokeVoidAsync("detach", _formElement);
                await _shortcuts.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone with the browser.
            }
        }

        _self?.Dispose();
        _exclusive.Dispose();
        await base.DisposeAsyncCore();
    }

    protected void OnBeforeInternalNavigation(LocationChangingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (GuardsNavigation)
        {
            context.PreventNavigation();
            PendingNavigation = context.TargetLocation;
        }
    }

    /// <summary>"Leave" with unsaved changes: they are discarded, draft included.</summary>
    protected async Task LeaveAsync()
    {
        var target = PendingNavigation;
        PendingNavigation = null;
        _leaving = true;
        _autosave?.Cancel();
        await DeleteDraftAsync();
        if (target is not null)
        {
            Navigation.NavigateTo(target);
        }
    }

    protected Task ConfirmCancelAsync() => CancelAsync();

    protected async Task SubmitAsync() => await SaveAsync();

    /// <summary>Discards my edits and shows the record as it is now ("Reload" on the banner).</summary>
    protected async Task ReloadAsync()
    {
        _autosave?.Cancel();
        await DeleteDraftAsync();
        await ExclusiveAsync(() => RefreshAsync(RemoteChange));
    }

    protected void CloseHistory() => History = null;

    protected void KeepMine()
    {
        if (RemoteChange?.Kind != ChangeKind.Deleted)
        {
            RemoteChange = null;
        }
    }

    protected async Task UseTheirsAsync()
    {
        if (Conflict is null)
        {
            return;
        }

        await ReplaceModelAsync(Conflict.Theirs);
        Conflict = null;
        Baseline();
        _autosave?.Cancel();
        await DeleteDraftAsync();
    }

    protected async Task ApplyMineAsync(IReadOnlySet<string> keepTheirs)
    {
        if (Conflict is null || _original is null)
        {
            return;
        }

        var server = Clone(Conflict.Theirs);
        var merged = (T)ConflictMerge.Apply(_original, ViewModel.Model!, Conflict.Theirs, keepTheirs).Merged;
        await ReplaceModelAsync(merged);
        Conflict = null;
        _original = server;
        FormContext!.Snapshot(server);
    }

    /// <summary>
    /// Restores the draft onto the record as it is now: what I changed in the draft (relative to what the draft started
    /// from) is applied to a fresh copy, so changes others saved since are kept. A new record's draft is the record.
    /// </summary>
    protected async Task RestoreDraftAsync()
    {
        var offer = DraftOffer;
        DraftOffer = null;
        if (offer is null || DraftCodec.TryDeserialize<T>(ApplicationContext, offer.Payload) is not { } draft)
        {
            await DeleteDraftAsync();
            return;
        }

        if (draft is ITrackStatus { IsNew: true })
        {
            await ReplaceModelAsync(Flatten(draft));
        }
        else
        {
            if (await TryLoadAsync() is not { } fresh)
            {
                await CloseForLostAccessAsync();
                return;
            }

            var server = Clone(fresh);
            var draftStart = draft is IUndoableObject { EditLevel: > 0 } ? Undo(draft) : Clone(fresh);
            var merged = (T)ConflictMerge.Apply(draftStart, draft, fresh).Merged;
            await ReplaceModelAsync(merged);
            _original = server;
            FormContext!.Snapshot(server);
        }

        Announcer.Announce(Messages.Draft_Restored);
    }

    protected async Task DiscardDraftAsync()
    {
        DraftOffer = null;
        await DeleteDraftAsync();
    }

    private void OpenHistory(object model, string propertyName)
    {
        if (model is CslaStarter.Core.IEntityIdentity identity)
        {
            History = (identity.EntityType, identity.EntityKey, propertyName);
            StateHasChanged();
        }
    }

    private async Task OfferDraftAsync()
    {
        if (_draftKey is not { } key || await Drafts.GetAsync(key, DisposalToken) is not { } draft)
        {
            return;
        }

        // A draft written before a deploy that changed the class can't be read back: it is no draft.
        if (DraftCodec.TryDeserialize<T>(ApplicationContext, draft.Payload) is null)
        {
            await Drafts.DeleteAsync(key, DisposalToken);
            return;
        }

        DraftOffer = draft;
    }

    private async Task SaveDraftAsync()
    {
        if (IsClosed || !ViewModel.IsDirty || _draftKey is not { } key)
        {
            return;
        }

        try
        {
            var version = ViewModel.Model is IVersioned { Version: var v } ? v : (uint?)null;
            await Drafts.SaveAsync(key, version, DraftCodec.Serialize(ApplicationContext, ViewModel.Model!), CancellationToken.None);
        }
#pragma warning disable CA1031 // A draft is a convenience: failing to write one must never disturb the edit.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogDraftFailed(Logger, ex);
        }
    }

    // A new record gets a fresh id on every load, so its draft is kept under "new" for the type.
    private DraftKey? CurrentDraftKey() =>
        CurrentUser.UserId is { } me && EntityType is { Length: > 0 } type && EntityKey is { Length: > 0 } key
            ? new DraftKey(me, type, ViewModel.Model is ITrackStatus { IsNew: true } ? NewRecordKey : key)
            : null;

    private void StartLiveUpdates()
    {
        if (Topic is not { } topic)
        {
            return;
        }

        // Own changes too (a command run from this page): the version check below skips the form's own saves.
        Subscribe(topic, change => ExclusiveAsync(() => OnRemoteChangeAsync(change)), includeOwn: true);
        if (CurrentUser.UserId is { } me)
        {
            Subscribe(ChangeTopic.ForRecord(EntityChange.UserAccessType, me.ToString()), _ => ExclusiveAsync(OnAccessChangedAsync));
        }

        OnResync(() => ExclusiveAsync(OnResyncAsync));
        UpdatePresence();
    }

    private async Task OnRemoteChangeAsync(EntityChange change)
    {
        if (IsClosed || ViewModel.Model is null)
        {
            return;
        }

        if (change.Kind != ChangeKind.Deleted && change.Version is { } version && ViewModel.Model is IVersioned { Version: var mine } && version == mine)
        {
            return;
        }

        if (change.Kind == ChangeKind.Deleted || ViewModel.IsDirty)
        {
            RemoteChange = change;
            StateHasChanged();
            return;
        }

        await RefreshAsync(change);
    }

    private async Task OnResyncAsync()
    {
        if (IsClosed || ViewModel.Model is not IVersioned { Version: var mine })
        {
            return;
        }

        var fresh = await TryLoadAsync();
        if (fresh is null)
        {
            await CloseForLostAccessAsync();
        }
        else if (fresh is IVersioned { Version: var theirs } && theirs != mine)
        {
            await OnRemoteChangeAsync(new EntityChange(EntityType!, EntityKey!, ChangeKind.Updated, theirs, null, string.Empty, null));
        }
    }

    private async Task OnAccessChangedAsync()
    {
        if (IsClosed)
        {
            return;
        }

        ViewModel.RefreshPermissions();
        if (await TryLoadAsync() is null)
        {
            await CloseForLostAccessAsync();
        }

        StateHasChanged();
    }

    private async Task CloseForLostAccessAsync()
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        _presence?.Dispose();
        _autosave?.Cancel();
        await DeleteDraftAsync();
        Toasts.Show(ToastKind.Warning, Messages.Live_AccessLost);
        Announcer.Announce(Messages.Live_AccessLost, assertive: true);

        // The record's values leave the page before the parent hears it closed.
        StateHasChanged();
        await Closed.InvokeAsync();
    }

    // Loads the record into a separate copy first: the form's model is only replaced once the load worked, and a record
    // that disappeared (or became hidden) closes the form instead.
    private async Task RefreshAsync(EntityChange? cause)
    {
        var old = ViewModel.Model!;
        T? fresh = null;
        var loaded = await RunAsync("refresh", async _ => fresh = await TryLoadAsync());
        if (!loaded)
        {
            return;
        }

        if (fresh is null)
        {
            await CloseForLostAccessAsync();
            return;
        }

        await ReplaceModelAsync(fresh);
        RemoteChange = null;
        var changed = FieldDiff.Between(fresh, old).Where(d => d.Path.Length == 0).Select(d => d.Property).ToList();
        Baseline();
        FormContext!.Highlight(changed);
        if (cause is { ChangedByName.Length: > 0 } && changed.Count > 0)
        {
            Announcer.Announce(Messages.Live_UpdatedBy.Replace("{0}", cause.ChangedByName, StringComparison.Ordinal));
        }

        UpdatePresence();
        StateHasChanged();
        _ = EndHighlightLaterAsync();
    }

    // The record as the server has it now, or null when the user may no longer see it.
    private async Task<T?> TryLoadAsync()
    {
        try
        {
            return await Load();
        }
#pragma warning disable CA1031 // Classified below; anything but "gone" or "denied" is rethrown.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (BusinessErrors.Classify(ex).Kind is BusinessErrorKind.NotFound or BusinessErrorKind.AccessDenied)
            {
                return null;
            }

            throw;
        }
    }

    private async Task<bool> OpenConflictAsync()
    {
        if (_original is null || await TryLoadAsync() is not { } theirs)
        {
            return false;
        }

        var mine = ViewModel.Model!;
        var bothChanged = ConflictMerge.Apply(_original, mine, Clone(theirs)).BothChanged;
        Conflict = new ConflictState(theirs, FieldDiff.Between(theirs, mine), bothChanged);
        StateHasChanged();
        return true;
    }

    private async Task ReplaceModelAsync(T model)
    {
        await ViewModel.RefreshAsync(() => Task.FromResult<T?>(model));
        UpdatePresence();
        StateHasChanged();
    }

    private Task DeleteDraftAsync() => _draftKey is { } key ? Drafts.DeleteAsync(key, CancellationToken.None) : Task.CompletedTask;

    // The object as the server has it: the baseline for "changed" markers, Cancel, and merging after a conflict.
    private void Baseline()
    {
        _original = ViewModel.Model is { } model ? Flatten(model) : null;
        FormContext?.Snapshot();
    }

    private T Clone(T model) => (T)ObjectCloner.GetInstance(ApplicationContext).Clone(model)!;

    private static T Flatten(T model) => (T)ConflictMerge.Flatten(model);

    // A copy of the draft as it was before its edits: CSLA's undo stack, which the draft carries, rolled back.
    private T Undo(T draft)
    {
        var copy = Clone(draft);
        while (copy is IUndoableObject { EditLevel: > 0 } and ISupportUndo undo)
        {
            undo.CancelEdit();
        }

        return copy;
    }

    // Saves, deletes, refreshes and live updates one at a time: a notice can't land in the middle of a save.
    private async Task<TResult> ExclusiveAsync<TResult>(Func<Task<TResult>> work)
    {
        await _exclusive.WaitAsync(DisposalToken);
        try
        {
            return await work();
        }
        finally
        {
            if (!IsDisposed)
            {
                _exclusive.Release();
            }
        }
    }

    private async Task ExclusiveAsync(Func<Task> work) => await ExclusiveAsync(async () =>
    {
        await work();
        return true;
    });

    private void UpdatePresence()
    {
        if (Topic is not { } topic || CurrentUser.UserId is not { } me || IsClosed || IsDisposed)
        {
            return;
        }

        var mode = ViewModel.IsDirty ? PresenceMode.Editing : PresenceMode.Viewing;
        if (mode != _presenceMode)
        {
            var previous = _presence;
            _presence = Presence.Join(topic, new PresenceEntry(me, CurrentUser.DisplayName, OriginId, mode));
            _presenceMode = mode;
            previous?.Dispose();
        }
    }

    private void OnModelChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            if (ViewModel.IsDirty)
            {
                _autosave?.Touch();
            }

            UpdatePresence();
            StateHasChanged();
        });
    }

    private async Task EndHighlightLaterAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), Time);
        RenderLater();
    }

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
    private void RenderLater() => _ = InvokeAsync(() =>
    {
        if (!IsDisposed)
        {
            StateHasChanged();
        }
    });

    [LoggerMessage(EventId = 4100, Level = LogLevel.Warning, Message = "Saving a draft failed; the edits are still in the form.")]
    private static partial void LogDraftFailed(ILogger logger, Exception exception);

    protected sealed record ConflictState(T Theirs, IReadOnlyList<FieldDifference> Differences, IReadOnlyList<FieldDifference> BothChanged);
}
