using CslaStarter.Core.Sessions;
using Microsoft.Extensions.Options;

namespace CslaStarter.Infrastructure.Sessions;

/// <summary>
/// Tracks the last real user activity (reported by the session-timeout component from keyboard and pointer input)
/// and moves Active → Warning → Expired. Expired is final. <see cref="StateChanged"/> is raised on a timer thread:
/// components must marshal to their renderer with InvokeAsync.
/// </summary>
public sealed class SessionTimeoutMonitor : IDisposable, ISessionActivity
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(15);

    private readonly TimeProvider _time;
    private readonly SessionOptions _options;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private DateTimeOffset _lastActivity;
    private SessionState _state = SessionState.Active;

    public SessionTimeoutMonitor(TimeProvider time, IOptions<SessionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _time = time;
        _options = options.Value;
        if (_options.WarningAfter <= TimeSpan.Zero || _options.WarningAfter >= _options.SignOutAfter)
        {
            throw new InvalidOperationException("Session:WarningAfter must be positive and shorter than Session:SignOutAfter.");
        }

        _lastActivity = time.GetUtcNow();
        _timer = time.CreateTimer(_ => Update(), null, CheckEvery, CheckEvery);
    }

    public event Action<SessionState>? StateChanged;

    public event Action<SessionPhase>? PhaseChanged;

    public SessionPhase Phase => ToPhase(State);

    public SessionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public TimeSpan Remaining
    {
        get
        {
            lock (_gate)
            {
                var remaining = _options.SignOutAfter - (_time.GetUtcNow() - _lastActivity);
                return _state == SessionState.Expired || remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }
    }

    public void RecordActivity()
    {
        lock (_gate)
        {
            if (_state == SessionState.Expired)
            {
                return;
            }

            _lastActivity = _time.GetUtcNow();
        }

        Update();
    }

    public void Dispose() => _timer.Dispose();

    private void Update()
    {
        SessionState changedTo;
        lock (_gate)
        {
            if (_state == SessionState.Expired)
            {
                return;
            }

            var idle = _time.GetUtcNow() - _lastActivity;
            var state = idle >= _options.SignOutAfter ? SessionState.Expired
                : idle >= _options.WarningAfter ? SessionState.Warning
                : SessionState.Active;
            if (state == _state)
            {
                return;
            }

            _state = changedTo = state;
        }

        StateChanged?.Invoke(changedTo);
        PhaseChanged?.Invoke(ToPhase(changedTo));
    }

    private static SessionPhase ToPhase(SessionState state) => state switch
    {
        SessionState.Warning => SessionPhase.Warning,
        SessionState.Expired => SessionPhase.Expired,
        _ => SessionPhase.Active,
    };
}
