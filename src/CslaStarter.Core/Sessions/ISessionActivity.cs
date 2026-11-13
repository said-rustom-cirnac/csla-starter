namespace CslaStarter.Core.Sessions;

public enum SessionPhase
{
    Active,
    Warning,
    Expired,
}

/// <summary>
/// The circuit's inactivity clock, as components see it: real user input (keyboard, pointer) resets it; Warning comes
/// before Expired, and Expired is final. <see cref="PhaseChanged"/> is raised off the renderer.
/// </summary>
public interface ISessionActivity
{
    SessionPhase Phase { get; }

    TimeSpan Remaining { get; }

    event Action<SessionPhase>? PhaseChanged;

    void RecordActivity();
}
