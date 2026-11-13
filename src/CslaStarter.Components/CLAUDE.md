# CslaStarter.Components

The reusable `Csla*` component family. Every component inherits `CslaBaseComponent`. Fields bind to CSLA properties and take their label, errors, required marker, length limit and permissions from the business object.

**May reference:** Core, Business, Csla.Blazor (and the shared ASP.NET Core framework). Never Data or Infrastructure.

## Layout

| Folder | Holds |
|---|---|
| `Base/` | `CslaBaseComponent`, `CslaBaseComponent<T>` |
| `Forms/` | `CslaForm`, `AppViewModel`, the form context and state, `FieldHints`, `FieldDiff`, `ConflictMerge` |
| `Fields/` | the GCDS field components and `FieldValue` |
| `Buttons/`, `Dialogs/`, `Notifications/`, `Live/`, `History/`, `Tables/`, `Security/`, `Session/`, `Localization/` | the rest of the family |
| `Drafts/` | autosave and the flush registry |
| `Services/` | toasts, the live announcer, the circuit origin |
| `Interop/` + `wwwroot/` | GCDS events, the JS initializer, shortcuts.js and session.js; `gcds/` (written by `scripts/update-gcds.sh` and committed; never edit it by hand) |

`AddCslaStarterComponents()` registers the per-circuit services.

## Rules

- Each component is `X.razor` (markup only) plus `X.razor.cs` (code). Tests enforce both halves.
- Never validate or check permissions here. Read them from CSLA's `IPropertyInfo` and the view model.
- One change subscription per form or table. Fields never subscribe.

## GC Design System traps

- Every field sets `validate-on="other"`, and forms set `novalidate`, so CSLA is the only validator.
- Never bind a changing `class` on a GCDS element. Stencil's `hydrated` class would be lost and the element would disappear. Put state on a wrapper instead.
- Object properties (`options`, `error-links`, `columns`) are passed as JSON strings.
- GCDS forces `disabled=false` when `required` is set, so a read-only or no-write field drops `required`.
- Give every field host an `id`, so error-summary links can focus it.
- Custom events (`gcdsinput`, `gcdschange`, `gcdsblur`, `gcdsfocus`, `gcdsclick`) are registered in the JS initializer. Their names differ from browser event names.

- Pure logic (diffs, merges, hints, debounce) goes in plain classes, so it can be tested without rendering.

Guides: [components](../../docs/claude/components.md), [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
