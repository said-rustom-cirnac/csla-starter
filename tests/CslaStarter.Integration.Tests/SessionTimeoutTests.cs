using CslaStarter.Infrastructure.Sessions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Integration.Tests;

public sealed class SessionTimeoutTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void SessionTimeout_WarnsAt18Minutes()
    {
        using var monitor = Monitor();
        var states = new List<SessionState>();
        monitor.StateChanged += states.Add;

        _time.Advance(TimeSpan.FromMinutes(17));
        Assert.Equal(SessionState.Active, monitor.State);
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(SessionState.Warning, monitor.State);
        Assert.Equal([SessionState.Warning], states);
        Assert.Equal(TimeSpan.FromMinutes(2), monitor.Remaining);
    }

    [Fact]
    public void SessionTimeout_ExpiresAt20Minutes()
    {
        using var monitor = Monitor();

        _time.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(SessionState.Expired, monitor.State);
        Assert.Equal(TimeSpan.Zero, monitor.Remaining);
    }

    [Fact]
    public void SessionTimeout_ActivityResetsTheClock()
    {
        using var monitor = Monitor();

        _time.Advance(TimeSpan.FromMinutes(17));
        monitor.RecordActivity();
        _time.Advance(TimeSpan.FromMinutes(17));

        Assert.Equal(SessionState.Active, monitor.State);
    }

    [Fact]
    public void SessionTimeout_ActivityAfterExpiry_DoesNotRevive()
    {
        using var monitor = Monitor();

        _time.Advance(TimeSpan.FromMinutes(21));
        monitor.RecordActivity();

        Assert.Equal(SessionState.Expired, monitor.State);
    }

    private SessionTimeoutMonitor Monitor() => new(_time, Options.Create(new SessionOptions()));

    [Theory]
    [InlineData(20, 20)]
    [InlineData(25, 20)]
    [InlineData(0, 20)]
    public void SessionOptions_WarningMustComeBeforeSignOut(int warningMinutes, int signOutMinutes)
    {
        var options = Options.Create(new SessionOptions { WarningAfter = TimeSpan.FromMinutes(warningMinutes), SignOutAfter = TimeSpan.FromMinutes(signOutMinutes) });

        Assert.Throws<InvalidOperationException>(() => new SessionTimeoutMonitor(_time, options));
    }
}
