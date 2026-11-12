# CslaStarter.Components

The reusable `Csla*` component family. Every component inherits `CslaBaseComponent`. Fields bind to CSLA properties and take their label, errors, required marker, length limit and permissions from the business object.

**May reference:** Core, Business, Csla.Blazor (and the shared ASP.NET Core framework). Never Data or Infrastructure.

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

Guides: [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
