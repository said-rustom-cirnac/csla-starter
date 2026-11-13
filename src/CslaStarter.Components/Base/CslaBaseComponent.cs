using System.Diagnostics;
using System.Globalization;
using CslaStarter.Business.Errors;
using CslaStarter.Business.Security;
using CslaStarter.Components.Services;
using CslaStarter.Components.Telemetry;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ApplicationContext = Csla.ApplicationContext;

namespace CslaStarter.Components.Base;

/// <summary>
/// What every Csla* component shares: a stable id, a token cancelled on dispose, operations that handle busy state,
/// tracing and errors in one place, toasts and screen-reader announcements, and change subscriptions that render on
/// the circuit and end with the component.
/// </summary>
public abstract partial class CslaBaseComponent : ComponentBase, IAsyncDisposable
{
    private static long _lastId;

    private readonly CancellationTokenSource _disposal = new();
    private readonly List<IDisposable> _subscriptions = [];
    private int _busy;
    private bool _disposed;

    protected CslaBaseComponent() =>
        ComponentId = "csla-" + Interlocked.Increment(ref _lastId).ToString(CultureInfo.InvariantCulture);

    /// <summary>Stable for the component's life; use it for DOM ids and aria links.</summary>
    protected string ComponentId { get; }

    /// <summary>Cancelled when the component is disposed (the user navigated away).</summary>
    protected CancellationToken DisposalToken => _disposal.Token;

    protected bool IsBusy => Volatile.Read(ref _busy) > 0;

    [Inject]
    protected ICurrentUser CurrentUser { get; set; } = default!;

    [Inject]
    protected IToastService Toasts { get; set; } = default!;

    [Inject]
    protected ILiveAnnouncer Announcer { get; set; } = default!;

    [Inject]
    protected ApplicationContext ApplicationContext { get; set; } = default!;

    /// <summary>This circuit's id in change notices and presence.</summary>
    protected string OriginId => Origin.Id;

    /// <summary>The entity type this component works on, for telemetry tags; null when it has none.</summary>
    protected virtual string? TelemetryEntityType => null;

    [Inject]
    private IEntityChangeHub ChangeHub { get; set; } = default!;

    [Inject]
    private CircuitOrigin Origin { get; set; } = default!;

    [Inject]
    private ISecurityEventLog SecurityEvents { get; set; } = default!;

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _disposal.CancelAsync();
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        await DisposeAsyncCore();
        _disposal.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        // Every data portal call this circuit makes carries its origin, so its own change notices can be recognized.
        ApplicationContext.ClientContext[CslaDataAccessContext.OriginKey] = Origin.Id;
        base.OnInitialized();
    }

    /// <summary>
    /// Calls <paramref name="handler"/> on this circuit's renderer for each change on <paramref name="topic"/> made
    /// elsewhere (this circuit's own changes are skipped, except access changes). Ends when the component is disposed.
    /// </summary>
    protected IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var origin = Origin.Id;
        return Track(ChangeHub.Subscribe(topic, change =>
        {
            if (change.OriginId != origin || change.Kind == ChangeKind.AccessChanged)
            {
                Dispatch(() => handler(change));
            }

            return Task.CompletedTask;
        }));
    }

    /// <summary>Calls <paramref name="handler"/> on the renderer after the backplane reconnected (notices may have been missed).</summary>
    protected IDisposable OnResync(Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(ChangeHub.OnResync(() =>
        {
            Dispatch(handler);
            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Runs an operation: busy while it runs, traced as <paramref name="operation"/>, and on failure classified and shown
    /// (toast and announcement; access denials are also logged as security events). Returns whether it succeeded.
    /// </summary>
    protected async Task<bool> RunAsync(string operation, Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var activity = ComponentTelemetry.Source.StartActivity(operation);
        activity?.SetTag("component", GetType().Name);
        activity?.SetTag("entity.type", TelemetryEntityType);
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _busy);
        StateHasChanged();
        try
        {
            await work(DisposalToken);
            return true;
        }
#pragma warning disable CA1031 // Every failure becomes a classified, user-facing error.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var error = BusinessErrors.Classify(ex, Activity.Current?.TraceId.ToString());
            if (error.Kind != BusinessErrorKind.Cancelled && !_disposed)
            {
                activity?.SetStatus(ActivityStatusCode.Error, error.Kind.ToString());
                await OnErrorAsync(operation, error);
            }

            return false;
        }
        finally
        {
            Interlocked.Decrement(ref _busy);
            ComponentTelemetry.Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new("operation", operation), new("component", GetType().Name));
            if (!_disposed)
            {
                StateHasChanged();
            }
        }
    }

    /// <summary>Shows a failed operation. Override to handle a kind yourself (forms open the conflict dialog), then call base.</summary>
    protected virtual async Task OnErrorAsync(string operation, BusinessError failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Kind == BusinessErrorKind.AccessDenied)
        {
            await SecurityEvents.WriteAsync(
                new SecurityEvent(SecurityEventKind.AccessDenied, CurrentUser.UserId, CurrentUser.DisplayName, $"{operation} {TelemetryEntityType}".TrimEnd(), Activity.Current?.TraceId.ToString()),
                CancellationToken.None);
        }
        else if (failure.Kind == BusinessErrorKind.Unexpected)
        {
            LogUnexpected(LoggerFactory.CreateLogger(GetType()), failure.Exception, operation);
        }

        Toasts.Show(ToastKind.Error, failure.Message);
        Announcer.Announce(failure.Message, assertive: true);
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    private IDisposable Track(IDisposable subscription)
    {
        _subscriptions.Add(subscription);
        return subscription;
    }

    // Hub callbacks arrive on the backplane's thread: hop onto the circuit and never let a failure escape.
    private void Dispatch(Func<Task> work) => _ = DispatchAsync(work);

    private async Task DispatchAsync(Func<Task> work)
    {
        try
        {
            await InvokeAsync(async () =>
            {
                if (!_disposed)
                {
                    await work();
                }
            });
        }
#pragma warning disable CA1031 // A failed refresh must not take the circuit down; it is logged.
        catch (Exception ex) when (ex is not ObjectDisposedException)
#pragma warning restore CA1031
        {
            LogLiveUpdateFailed(LoggerFactory.CreateLogger(GetType()), ex);
        }
        catch (ObjectDisposedException)
        {
            // The circuit ended between the notice and the hop.
        }
    }

    [LoggerMessage(EventId = 4000, Level = LogLevel.Error, Message = "Operation {Operation} failed unexpectedly.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string operation);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Applying a live update failed.")]
    private static partial void LogLiveUpdateFailed(ILogger logger, Exception exception);
}
