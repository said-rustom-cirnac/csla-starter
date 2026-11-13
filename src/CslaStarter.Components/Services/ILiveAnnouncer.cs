namespace CslaStarter.Components.Services;

/// <summary>
/// Messages for screen readers ("Saved", "Updated by Jane", session warnings). <c>CslaLiveRegion</c> renders them in an
/// aria-live region. Assertive messages interrupt (errors); the rest are polite.
/// </summary>
public interface ILiveAnnouncer
{
    event Action<string, bool>? Announced;

    void Announce(string message, bool assertive = false);
}

public sealed class LiveAnnouncer : ILiveAnnouncer
{
    public event Action<string, bool>? Announced;

    public void Announce(string message, bool assertive = false) => Announced?.Invoke(message, assertive);
}
