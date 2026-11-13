---
name: new-component
description: Use when adding a Csla* Blazor component or a page built from them. Covers the base class, the .razor/.razor.cs split, GCDS interop and bUnit tests. See docs/claude/components.md.
---

# New component or page

Read [components](../../../docs/claude/components.md) first.

**A new component** (in `CslaStarter.Components`):
1. Inherit `CslaBaseComponent`, or `CslaFieldBase<TValue>` for a field.
2. `X.razor` holds the bare GCDS element with its attributes and events (`@ongcdsinput`, `@ongcdschange`, `@ongcdsclick`). `X.razor.cs` holds all the code. No `@code` blocks.
3. Read labels, errors, required, length limit, permissions and busy state from the object (`FieldHints`). Never repeat a rule in markup.
4. **Async work:**
   - Run it through `RunAsync(name, work)` (busy, tracing, errors).
   - Subscribe to changes with `Subscribe(topic, handler)`. Handlers run on the renderer and end with the component.
   - Long-lived JS (`IJSObjectReference`) and `DotNetObjectReference` are released in `DisposeAsyncCore`.
5. **GCDS traps:**
   - Never bind a changing `class` on a GCDS host.
   - Object props are JSON strings.
   - A no-write field drops `required`.
   - Every field host has an `id`.
6. **bUnit test** in `CslaStarter.Components.Tests` with `ComponentTestContext`. Find and trigger inside `InvokeAsync`, and await `TriggerEventAsync`.

**A new page** (in `CslaStarter.Web`): follow [add a page](../../../docs/claude/recipes/add-a-page.md). The page only composes components: `CslaForm` with fields and buttons, `CslaTable` with columns. Inject `Csla.IDataPortal<T>` by its full name.

**Run** `dotnet test` and `dotnet format --verify-no-changes`.
