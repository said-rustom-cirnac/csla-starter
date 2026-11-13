using CslaStarter.Components.Services;

namespace CslaStarter.Components.Notifications;

/// <summary>Shows the circuit's toasts (gcds-notice). Place one in the layout.</summary>
public partial class CslaToastHost
{
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Toasts.Changed += OnChanged;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        Toasts.Changed -= OnChanged;
        return base.DisposeAsyncCore();
    }

    private static string NoticeType(ToastKind kind) => kind switch
    {
        ToastKind.Success => "success",
        ToastKind.Warning => "warning",
        ToastKind.Error => "danger",
        _ => "info",
    };

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);
}
