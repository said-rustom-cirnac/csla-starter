namespace CslaStarter.Components.Drafts;

/// <summary>
/// The circuit's open forms (scoped). Before anything reloads the page or ends the session (language switch, session
/// timeout), <see cref="FlushAllAsync"/> writes every dirty form's draft, so nothing typed is lost.
/// </summary>
public sealed class DraftFlushRegistry
{
    private readonly List<IDraftFlusher> _flushers = [];

    public IDisposable Register(IDraftFlusher flusher)
    {
        ArgumentNullException.ThrowIfNull(flusher);
        _flushers.Add(flusher);
        return new Registration(this, flusher);
    }

    public async Task FlushAllAsync()
    {
        foreach (var flusher in _flushers.ToList())
        {
            await flusher.FlushDraftAsync();
        }
    }

    private sealed class Registration(DraftFlushRegistry registry, IDraftFlusher flusher) : IDisposable
    {
        public void Dispose() => registry._flushers.Remove(flusher);
    }
}
