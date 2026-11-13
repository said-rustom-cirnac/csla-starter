// Reports real user activity (keyboard, pointer) to the session-timeout component, at most every 30 seconds.
// Circuit traffic (server-pushed updates) never counts as activity.

const throttle = 30_000;

export function watchActivity(dotnet) {
    let last = 0;
    const report = () => {
        const now = Date.now();
        if (now - last >= throttle) {
            last = now;
            dotnet.invokeMethodAsync('ReportActivity');
        }
    };
    for (const type of ['keydown', 'pointerdown', 'wheel', 'touchstart']) {
        document.addEventListener(type, report, { passive: true, capture: true });
    }
}

// Posts the hidden sign-out form (POST with its antiforgery token).
export function submit(form) {
    form.submit();
}
