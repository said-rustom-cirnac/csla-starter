// Ctrl+S (or Cmd+S) saves and Esc cancels, scoped to one form. Esc is left alone while a dialog is open.

export function attach(form, dotnet) {
    const onKey = e => {
        if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
            e.preventDefault();
            dotnet.invokeMethodAsync('ShortcutSave');
        } else if (e.key === 'Escape' && !e.defaultPrevented && !document.querySelector('dialog[open]')) {
            dotnet.invokeMethodAsync('ShortcutCancel');
        }
    };
    form.addEventListener('keydown', onKey);
    form.cslaShortcuts = onKey;
}

export function detach(form) {
    if (form && form.cslaShortcuts) {
        form.removeEventListener('keydown', form.cslaShortcuts);
        delete form.cslaShortcuts;
    }
}
