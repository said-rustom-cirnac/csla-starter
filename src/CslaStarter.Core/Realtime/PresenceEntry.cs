namespace CslaStarter.Core.Realtime;

public enum PresenceMode
{
    Viewing,
    Editing,
}

/// <summary>One circuit holding a record open. A user may have several circuits.</summary>
public sealed record PresenceEntry(Guid UserId, string DisplayName, string CircuitId, PresenceMode Mode);
