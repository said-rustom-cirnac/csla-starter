using System.Runtime.ExceptionServices;
using Csla.Core;
using CslaStarter.Business.Base;
using CslaStarter.Business.Errors;
using CslaStarter.Components.Base;
using CslaStarter.Components.Services;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Forms;

/// <summary>
/// Edits one business object: loads it through <see cref="Load"/>, cascades a <see cref="CslaFormContext{T}"/> to its
/// fields, and saves or cancels (undo) through an <see cref="AppViewModel{T}"/>. It keeps the record live:
/// <list type="bullet">
/// <item>someone else's save refreshes a clean editor (changed fields highlighted, "Updated by Jane" announced) and
/// shows a Reload / Keep mine banner on a dirty one;</item>
/// <item>after a backplane reconnect it re-checks the version;</item>
/// <item>if the user loses access, it closes with a message and discards the draft;</item>
/// <item>a save that loses the race opens the conflict dialog;</item>
/// <item>presence says who else has the record open.</item>
/// </list>
/// Errors show once a field is touched or a save is attempted; CSLA is the only validator (the form is <c>novalidate</c>).
/// </summary>
public partial class CslaForm<T> : CslaBaseComponent<T>
    where T : class
{
    private T? _original;
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

    public AppViewModel<T> ViewModel { get; private set; } = default!;

    public bool IsDirty => ViewModel?.IsDirty == true;

    public bool IsClosed { get; private set; }

    protected CslaFormContext<T>? FormContext { get; private set; }

    protected override T? Current => ViewModel?.Model;

    /// <summary>Someone else's change, waiting for the user to reload or keep their edits.</summary>
    protected EntityChange? RemoteChange { get; private set; }

    protected ConflictState? Conflict { get; private set; }

    protected ChangeTopic? Topic => EntityType is { Length: > 0 } type && EntityKey is { Length: > 0 } key ? ChangeTopic.ForRecord(type, key) : null;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private IPresenceTracker Presence { get; set; } = default!;

    [Inject]
    private IDraftStore Drafts { get; set; } = default!;

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
            Baseline();
            RemoteChange = null;
            Toasts.Show(ToastKind.Success, Messages.Toast_Saved);
            Announcer.Announce(Messages.Toast_Saved);
            UpdatePresence();
            await Saved.InvokeAsync(ViewModel.Model!);
        }

        return saved;
    }

    /// <summary>Undoes every change since the object was loaded or last saved.</summary>
    public async Task CancelAsync()
    {
        await ViewModel.CancelAsync();
        FormContext?.Snapshot();
        UpdatePresence();
        StateHasChanged();
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
        if (loaded)
        {
            // Listen before the fields first render, so no change made after the load can slip past.
            StartLiveUpdates();
            FormContext = new CslaFormContext<T>(ComponentId, ViewModel, Time);
            Baseline();
        }
    }

    protected override async Task OnErrorAsync(string operation, BusinessError failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Kind == BusinessErrorKind.Conflict && operation == "save" && await OpenConflictAsync())
        {
            return;
        }

        await base.OnErrorAsync(operation, failure);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _presence?.Dispose();
        return base.DisposeAsyncCore();
    }

    protected async Task SubmitAsync() => await SaveAsync();

    /// <summary>Discards my edits and shows the record as it is now ("Reload" on the banner).</summary>
    protected Task ReloadAsync() => RefreshAsync(RemoteChange);

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

    private void StartLiveUpdates()
    {
        if (Topic is not { } topic)
        {
            return;
        }

        Subscribe(topic, OnRemoteChangeAsync);
        if (CurrentUser.UserId is { } me)
        {
            Subscribe(ChangeTopic.ForRecord(EntityChange.UserAccessType, me.ToString()), _ => OnAccessChangedAsync());
        }

        OnResync(OnResyncAsync);
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
        if (fresh is IVersioned { Version: var theirs } && theirs != mine)
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
            IsClosed = true;
            _presence?.Dispose();
            await DeleteDraftAsync();
            Toasts.Show(ToastKind.Warning, Messages.Live_AccessLost);
            Announcer.Announce(Messages.Live_AccessLost, assertive: true);
            await Closed.InvokeAsync();
        }

        StateHasChanged();
    }

    private async Task RefreshAsync(EntityChange? cause)
    {
        var old = ViewModel.Model!;
        var refreshed = await RunAsync("refresh", async _ =>
        {
            var before = ViewModel.Exception;
            await ViewModel.RefreshAsync(async () => await Load());
            ThrowIfFailed(before);
        });
        if (!refreshed)
        {
            return;
        }

        RemoteChange = null;
        var changed = FieldDiff.Between(ViewModel.Model!, old).Where(d => d.Path.Length == 0).Select(d => d.Property).ToList();
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

    private Task DeleteDraftAsync() =>
        CurrentUser.UserId is { } me && EntityType is { } type && EntityKey is { } key
            ? Drafts.DeleteAsync(new DraftKey(me, type, key), CancellationToken.None)
            : Task.CompletedTask;

    // The object as the server has it: the baseline for "changed" markers and for merging after a conflict.
    private void Baseline()
    {
        _original = ViewModel.Model is { } model ? Clone(model) : null;
        FormContext?.Snapshot();
    }

    private T Clone(T model) => (T)ObjectCloner.GetInstance(ApplicationContext).Clone(model)!;

    private void UpdatePresence()
    {
        if (Topic is not { } topic || CurrentUser.UserId is not { } me || IsClosed)
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
    private void RenderLater() => _ = InvokeAsync(StateHasChanged);

    protected sealed record ConflictState(T Theirs, IReadOnlyList<FieldDifference> Differences, IReadOnlyList<FieldDifference> BothChanged);
}
