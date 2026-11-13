// Blazor JS initializer for CslaStarter.Components (loaded automatically by the Blazor Web App script).
// Registers the GC Design System's custom events under names that differ from the browser event names.

const valueOf = detail => {
    if (detail === null || detail === undefined) return null;
    if (Array.isArray(detail)) return detail.join(',');
    if (typeof detail === 'object') return detail.value === undefined ? null : String(detail.value);
    return String(detail);
};

const withValue = e => ({ value: valueOf(e.detail) });
const empty = () => ({});

export function afterWebStarted(blazor) {
    blazor.registerCustomEventType('gcdsinput', { browserEventName: 'gcdsInput', createEventArgs: withValue });
    blazor.registerCustomEventType('gcdschange', { browserEventName: 'gcdsChange', createEventArgs: withValue });
    blazor.registerCustomEventType('gcdsblur', { browserEventName: 'gcdsBlur', createEventArgs: empty });
    blazor.registerCustomEventType('gcdsfocus', { browserEventName: 'gcdsFocus', createEventArgs: empty });
    blazor.registerCustomEventType('gcdsclick', { browserEventName: 'gcdsClick', createEventArgs: withValue });
}
