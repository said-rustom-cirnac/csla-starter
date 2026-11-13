// Turns a rendered <dialog open> into a modal one: focus moves in (to the [autofocus] control, else the first one),
// the page behind goes inert, and when it closes focus goes back to where it was. Rendered open, the dialog still
// shows if this script never runs.
const openers = new WeakMap();

export function showModal(dialog) {
    if (!dialog || !dialog.isConnected) {
        return;
    }

    openers.set(dialog, document.activeElement);
    if (dialog.open) {
        dialog.close();
    }

    dialog.showModal();
}

export function close(dialog) {
    const opener = dialog ? openers.get(dialog) : null;
    if (dialog?.open) {
        dialog.close();
    }

    if (opener && opener.isConnected && typeof opener.focus === 'function') {
        opener.focus();
    }
}
