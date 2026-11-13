using CslaStarter.Core.Realtime;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Live;

/// <summary>"Jane is also editing this record": everyone else who has the record open, on any instance.</summary>
public partial class CslaPresenceIndicator
{
    private IDisposable? _watch;

    [Parameter, EditorRequired]
    public ChangeTopic Topic { get; set; }

    protected IReadOnlyList<PresenceEntry> Others { get; private set; } = [];

    [Inject]
    private IPresenceTracker Presence { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _watch ??= Presence.Watch(Topic, () => InvokeAsync(Refresh));
        Refresh();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _watch?.Dispose();
        return base.DisposeAsyncCore();
    }

    private static string Describe(PresenceEntry entry) =>
        (entry.Mode == PresenceMode.Editing ? Messages.Presence_Editing : Messages.Presence_Viewing).Replace("{0}", entry.DisplayName, StringComparison.Ordinal);

    private void Refresh()
    {
        Others = [.. Presence.Present(Topic).Where(p => p.UserId != CurrentUser.UserId)];
        StateHasChanged();
    }
}
