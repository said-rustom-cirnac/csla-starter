// Blazor JS initializer for CslaStarter.Components (loaded automatically by the Blazor Web App script).
// Loads the self-hosted GC Design System under the strict CSP (Stencil's injected styles carry the page's nonce),
// and registers its custom events under names that differ from the browser event names.

const valueOf = detail => {
    if (detail === null || detail === undefined) return null;
    if (Array.isArray(detail)) return detail.join(',');
    if (typeof detail === 'object') return detail.value === undefined ? null : String(detail.value);
    return String(detail);
};

const withValue = e => ({ value: valueOf(e.detail) });
const empty = () => ({});

export async function beforeWebStart() {
    try {
        const loader = await import('./gcds/loader/index.js');
        const nonce = document.querySelector('meta[name="csp-nonce"]')?.content;
        if (nonce) {
            loader.setNonce(nonce);
        }
        await loader.defineCustomElements(window);
    } catch (error) {
        console.warn('The GC Design System assets are missing: run scripts/update-gcds.sh.', error);
    }
}

export function afterWebStarted(blazor) {
    blazor.registerCustomEventType('gcdsinput', { browserEventName: 'gcdsInput', createEventArgs: withValue });
    blazor.registerCustomEventType('gcdschange', { browserEventName: 'gcdsChange', createEventArgs: withValue });
    blazor.registerCustomEventType('gcdsblur', { browserEventName: 'gcdsBlur', createEventArgs: empty });
    blazor.registerCustomEventType('gcdsfocus', { browserEventName: 'gcdsFocus', createEventArgs: empty });
    blazor.registerCustomEventType('gcdsclick', { browserEventName: 'gcdsClick', createEventArgs: withValue });
}
