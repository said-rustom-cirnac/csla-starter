namespace CslaStarter.Infrastructure.Sessions;

/// <summary>Idle session limits (ITSG-33 AC-11/AC-12), bound from the "Session" configuration section.</summary>
public sealed class SessionOptions
{
    public TimeSpan WarningAfter { get; set; } = TimeSpan.FromMinutes(18);

    public TimeSpan SignOutAfter { get; set; } = TimeSpan.FromMinutes(20);
}
