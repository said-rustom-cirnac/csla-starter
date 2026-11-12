using Microsoft.Extensions.Options;

namespace CslaStarter.Infrastructure.Sessions;

/// <summary>
/// Tracks the circuit's last user activity and moves Active → Warning → Expired. The session-timeout component
/// shows the warning and signs the user out on Expired. Expired is final.
/// </summary>
public sealed class SessionTimeoutMonitor : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(15);

    private readonly TimeProvider _time;
    private readonly SessionOptions _options;
    private readonly ITimer _timer;
    private DateTimeOffset _lastActivity;

    public SessionTimeoutMonitor(TimeProvider time, IOptions<SessionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _time = time;
        _options = options.Value;
        _lastActivity = time.GetUtcNow();
        _timer = time.CreateTimer(_ => Update(), null, CheckEvery, CheckEvery);
    }

    public event Action<SessionState>? StateChanged;

    public SessionState State { get; private set; } = SessionState.Active;

    public TimeSpan Remaining
    {
        get
        {
            var remaining = _options.SignOutAfter - (_time.GetUtcNow() - _lastActivity);
            return State == SessionState.Expired || remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public void RecordActivity()
    {
        if (State == SessionState.Expired)
        {
            return;
        }

        _lastActivity = _time.GetUtcNow();
        Update();
    }

    public void Dispose() => _timer.Dispose();

    private void Update()
    {
        if (State == SessionState.Expired)
        {
            return;
        }

        var idle = _time.GetUtcNow() - _lastActivity;
        var state = idle >= _options.SignOutAfter ? SessionState.Expired
            : idle >= _options.WarningAfter ? SessionState.Warning
            : SessionState.Active;
        if (state != State)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
