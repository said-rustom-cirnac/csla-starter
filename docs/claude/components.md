# Components

The `Csla*` family in `CslaStarter.Components`: Blazor components bound to CSLA business objects. Every component inherits `CslaBaseComponent`. Labels, errors, required markers, length limits, permissions and live updates all come from the business object, never from markup.

## The family

| Component | What it does |
|---|---|
| `CslaForm<T>` | Loads an object (`Load`), cascades its context to fields and buttons, saves and cancels (undo) through `AppViewModel<T>`. Adds the change banner, presence, drafts, conflict dialog, error summary, shortcuts and unsaved-changes guard by itself. |
| Fields | `CslaTextField`, `CslaTextArea`, `CslaNumberField<T>`, `CslaDateField`, `CslaSelect<T>`, `CslaCheckbox`, `CslaCheckboxGroup<T>`, `CslaRadioGroup<T>`, `CslaSensitiveField`. Bound with `For="() => form.Model.Prop"`; a field can bind a child (`() => task.Title`). |
| `CslaButton` | `Kind`: `Save`, `Cancel`, `Delete` (asks first), `New`, `Custom`. It follows the object's permissions, savable and busy state. |
| `CslaTable<TList,TItem>` | A server-paged list with `CslaTableColumn` children (sortable, hidden when not readable). It refreshes 500 ms after any change of `EntityType`. |
| `CslaHistory` | A record's (or one field's) change history; masked values show as "(hidden)". It shows what the record shows: the business type marked `[HistoryOf]` decides who may open it and which fields they see. |
| `CslaErrorSummary`, `CslaToastHost`, `CslaLiveRegion` | Errors with field links, toasts, and screen-reader announcements. The form renders the summary; the layout hosts the other two. |
| `CslaPresenceIndicator`, `CslaChangeBanner`, `CslaConflictDialog`, `CslaDraftPrompt`, `CslaConfirmDialog` | Live-update and dialog pieces used by the form. The conflict dialog masks `[Sensitive]` values. |
| `CslaModal` | The modal behind every dialog (dialog.js `showModal`): focus moves in (to the `autofocus` control; the safe one for a destructive choice) and back out, and Esc calls `OnCancel`. Ids come from `ComponentId`, so two dialogs never share one. |
| `CslaSessionTimeout`, `CslaLanguageToggle` | In the layout. Both write open forms' drafts before the page reloads or the session ends. |
| `CslaAuthorize` | Shows content to roles or permission holders. It only hides UI: the business objects enforce every rule. |

## The base contract (`CslaBaseComponent`)

- `ComponentId` (stable, for DOM ids), `DisposalToken`, `IsBusy`.
- `RunAsync(operation, work)` handles busy state, the trace span and the duration histogram. On failure it classifies the error through `BusinessErrors`:
  - **access denied:** a toast plus a security event;
  - **not found, conflict, invalid:** a localized message;
  - **anything else:** "Something went wrong" with the trace id as reference, and the details logged.
  - Override `OnErrorAsync` to handle a kind yourself (the form opens the conflict dialog on a conflict).
- `Subscribe(topic, handler, includeOwn)` and `OnResync(handler)` run the handler on the circuit's renderer and end with the component. Changes this circuit made itself are skipped unless `includeOwn`; access changes always arrive.
- `Toasts`, `Announcer`, `CurrentUser`, `ApplicationContext`, `Logger`, `WriteSecurityEventAsync`.
- The circuit's origin id (`CircuitOrigin`) is stamped into CSLA's `ClientContext`, so the data layer records it as each change's origin.

## Writing a form

```razor
<CslaForm T="ProjectEdit" Load="() => Portal.FetchAsync(Id)" Saved="OnSaved" Deleted="BackToList" Closed="BackToList" FieldHistory="true" Context="form">
    <CslaTextField For="() => form.Model.Code" />
    <CslaSelect TValue="int" For="() => form.Model.StatusCode" Options="@TypeOptions.From(statuses, form.Model.StatusCode)" />
    <CslaButton Kind="ButtonKind.Save" />
    <CslaButton Kind="ButtonKind.Cancel" />
    <CslaButton Kind="ButtonKind.Delete" />
</CslaForm>
```

- **Errors** show once a field is touched or a save was attempted. Save is enabled whenever there are edits the user may save and no rule is running; saving an invalid object (or Enter) opens the error summary.
- **One thing at a time:** saves, deletes, reloads and live updates queue behind each other. Every model replacement (cancel, reload, restore) goes through a copy flattened to edit level 0, and a failed delete leaves the object as it was.
- **Live updates:** someone else's save refreshes a clean form (changed fields are highlighted for 5 seconds and "Updated by Jane" is announced). A dirty form shows "Reload / Keep mine". A save that loses the race opens the conflict dialog: "Use theirs", or "Apply mine", which merges my changes, children included, onto their copy for review.
- **Drafts** are saved 2 seconds after the last edit while the form is dirty, and offered back when the record is opened again (with a warning if it changed since); a new record's draft is kept under "new". Restoring applies my draft's changes onto the record as it is now, so others' saves are kept. Drafts are deleted on save, cancel, reload, "Use theirs" or leaving.
- **Losing access** (removed as a member): the form closes with a message, raises `Closed`, and deletes the draft.
- **Shortcuts:** Ctrl+S saves; Esc asks before discarding unsaved changes. Leaving with unsaved changes asks first, except for a language switch or sign-out, which write the draft and go.
- **Buttons:** a `Custom` or `New` button's `OnClick` runs through the base component, so a failure shows as an error. Hide actions the user can't take with `CslaAuthorize` (commands authorize as `EditObject`).
- **Tables:** when rows can be selected, each row is a tab stop that Enter or Space selects; the selected row is `aria-current`.

## GC Design System

- **Assets:** self-hosted under `wwwroot/gcds`, fetched by `scripts/update-gcds.sh`: `@gcds-core/components` 1.7.0 and `@gcds-core/fonts` 1.1.1, integrity pinned in `scripts/gcds.integrity`. The script drops gcds.css's Google Fonts import (the CSP blocks it) and serves the fonts from `gcds/fonts.css`. Until it runs, GCDS elements stay inert and the browser console says so.
- **Under the CSP:** the JS initializer passes the page's `csp-nonce` to Stencil's `setNonce`, so its styles carry the nonce (`style-src` allows nonces, never `unsafe-inline`).
- **Events** are registered as `gcdsinput`, `gcdschange`, `gcdsblur`, `gcdsfocus` and `gcdsclick` (names that differ from the browser's). `GcdsEventArgs.Value` carries `detail`.
- **Traps:**
  - Never bind a changing `class` on a GCDS element; state goes on the wrapper (`data-changed`, `data-highlighted`).
  - Object props (`options`, `error-links`) are JSON strings.
  - A field the user can't write drops `required`: it is `readonly` on `gcds-input` and rendered as text elsewhere.
- **Tables** are semantic `<table>`s with `gcds-pagination`; `gcds-table` is client-side only.

## Testing

Use `ComponentTestContext` in `CslaStarter.Components.Tests` (see its `CLAUDE.md`):
- it renders against real business objects with fake DALs;
- `Hub` and `Time` drive live updates and debounces;
- `Drafts`, `SecurityEvents`, `AuditReader` and `Session` are fakes to assert on.

Find and trigger elements inside `host.InvokeAsync(...)`, and await `TriggerEventAsync`.
