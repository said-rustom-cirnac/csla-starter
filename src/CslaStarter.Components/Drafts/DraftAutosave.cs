namespace CslaStarter.Components.Drafts;

/// <summary>Saves a draft a fixed time after the last edit (the timer restarts on every edit).</summary>
public sealed class DraftAutosave : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Func<Task> _save;
    private readonly ITimer _timer;
    private int _pending;

    public DraftAutosave(TimeProvider time, TimeSpan delay, Func<Task> save)
    {
        ArgumentNullException.ThrowIfNull(time);
        _delay = delay;
        _save = save;
        _timer = time.CreateTimer(_ => _ = FireAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsPending => Volatile.Read(ref _pending) == 1;

    /// <summary>An edit happened: save <c>delay</c> from now, unless another edit comes first.</summary>
    public void Touch()
    {
        Volatile.Write(ref _pending, 1);
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Forgets the pending save (the record was saved, cancelled or reloaded).</summary>
    public void Cancel()
    {
        Volatile.Write(ref _pending, 0);
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Saves the pending draft now (before a language switch or a sign-out).</summary>
    public Task FlushAsync()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return FireAsync();
    }

    public void Dispose() => _timer.Dispose();

    private Task FireAsync() => Interlocked.Exchange(ref _pending, 0) == 1 ? _save() : Task.CompletedTask;
}
