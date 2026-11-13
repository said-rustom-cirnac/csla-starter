using CslaStarter.Components.Drafts;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Components.Tests.Drafts;

public sealed class DraftAutosaveTests
{
    private readonly FakeTimeProvider _time = new();
    private int _saves;

    [Fact]
    public void Autosave_DebouncesTo2SecondsAfterTheLastEdit()
    {
        using var autosave = Autosave();

        autosave.Touch();
        _time.Advance(TimeSpan.FromSeconds(1.5));
        autosave.Touch();
        _time.Advance(TimeSpan.FromSeconds(1.5));
        Assert.Equal(0, _saves);
        _time.Advance(TimeSpan.FromSeconds(0.6));

        Assert.Equal(1, _saves);
        Assert.False(autosave.IsPending);
    }

    [Fact]
    public void Autosave_Cancel_StopsThePendingSave()
    {
        using var autosave = Autosave();

        autosave.Touch();
        autosave.Cancel();
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(0, _saves);
    }

    [Fact]
    public async Task Autosave_Flush_SavesAPendingDraftNow()
    {
        using var autosave = Autosave();

        autosave.Touch();
        await autosave.FlushAsync();
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(1, _saves);
    }

    [Fact]
    public async Task FlushRegistry_FlushesEveryRegisteredForm()
    {
        var registry = new DraftFlushRegistry();
        var flushed = new List<string>();
        using var a = registry.Register(new Flusher(() => flushed.Add("a")));
        var b = registry.Register(new Flusher(() => flushed.Add("b")));
        b.Dispose();
        using var c = registry.Register(new Flusher(() => flushed.Add("c")));

        await registry.FlushAllAsync();

        Assert.Equal(["a", "c"], flushed);
    }

    private DraftAutosave Autosave() => new(_time, TimeSpan.FromSeconds(2), () =>
    {
        _saves++;
        return Task.CompletedTask;
    });

    private sealed class Flusher(Action flush) : IDraftFlusher
    {
        public Task FlushDraftAsync()
        {
            flush();
            return Task.CompletedTask;
        }
    }
}
