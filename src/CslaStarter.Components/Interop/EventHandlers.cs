using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Interop;

/// <summary>
/// The GC Design System's events, registered in <c>CslaStarter.Components.lib.module.js</c> under names that differ from
/// the browser's (gcdsinput for gcdsInput...), so they never fire twice or collide with built-in events.
/// </summary>
[EventHandler("ongcdsinput", typeof(GcdsEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("ongcdschange", typeof(GcdsEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("ongcdsblur", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("ongcdsfocus", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("ongcdsclick", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: false)]
public static class EventHandlers;

/// <summary>A GCDS event's <c>detail</c>: the field's value as text (several values are comma-separated).</summary>
public sealed class GcdsEventArgs : EventArgs
{
    public string? Value { get; set; }
}
