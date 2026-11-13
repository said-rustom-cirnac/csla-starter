
namespace CslaStarter.Components.Notifications;

/// <summary>The screen-reader live regions the announcer speaks through ("Saved", "Updated by Jane"). Place one in the layout.</summary>
public partial class CslaLiveRegion
{
    private string? _polite;
    private string? _assertive;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Announcer.Announced += OnAnnounced;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        Announcer.Announced -= OnAnnounced;
        return base.DisposeAsyncCore();
    }

    private void OnAnnounced(string message, bool assertive) => _ = InvokeAsync(() =>
    {
        if (assertive)
        {
            _assertive = message;
        }
        else
        {
            _polite = message;
        }

        StateHasChanged();
    });
}
