namespace CslaStarter.Components.Services;

/// <summary>One per circuit (scoped). Called on the circuit's renderer, so no locking.</summary>
public sealed class ToastService : IToastService
{
    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Toasts => _toasts;

    public event Action? Changed;

    public Toast Show(ToastKind kind, string message)
    {
        var toast = new Toast(Guid.NewGuid(), kind, message);
        _toasts.Add(toast);
        Changed?.Invoke();
        return toast;
    }

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }
}
