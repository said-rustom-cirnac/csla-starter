namespace CslaStarter.Components.Services;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error,
}

public sealed record Toast(Guid Id, ToastKind Kind, string Message);

/// <summary>The circuit's short notifications. <c>CslaToastHost</c> renders them; components only add them.</summary>
public interface IToastService
{
    IReadOnlyList<Toast> Toasts { get; }

    event Action? Changed;

    Toast Show(ToastKind kind, string message);

    void Dismiss(Guid id);
}
