namespace CslaStarter.Core.Realtime;

/// <summary>Who else has a record open, across instances. Entries expire when their instance stops sending heartbeats.</summary>
public interface IPresenceTracker
{
    /// <summary>Announces <paramref name="entry"/> on the topic until the returned handle is disposed.</summary>
    IDisposable Join(ChangeTopic topic, PresenceEntry entry);

    /// <summary>Everyone on the topic, one entry per user (Editing wins over Viewing).</summary>
    IReadOnlyList<PresenceEntry> Present(ChangeTopic topic);

    /// <summary>Calls <paramref name="changed"/> whenever the topic's presence changes.</summary>
    IDisposable Watch(ChangeTopic topic, Func<Task> changed);
}
