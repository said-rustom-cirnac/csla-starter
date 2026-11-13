# Add a page

For a business object `Widget` (`WidgetEdit` and `WidgetList`) that already exists ([add a feature](add-a-feature.md)):

1. **List page:** `Web/Components/Pages/<Feature>/WidgetListPage.razor` with `@page "/widgets"`, plus `.razor.cs`:
   - `<CslaTable TList="WidgetList" TItem="WidgetInfo" EntityType="Widget" Load="Load" RowSelected="Open">` with one `<CslaTableColumn TItem="WidgetInfo" Property="@nameof(WidgetInfo.Name)" Sortable="true" />` per column.
   - `Load(TableState state)` calls `IDataPortal<WidgetList>.FetchAsync(...)` with `state.Filter`, `state.SortBy`, `state.Descending`, `state.Page` and `state.PageSize`.
   - `WidgetInfo` implements `IEntityIdentity`, and `WidgetList` implements `IPagedList`.
2. **Edit page:** `WidgetEditPage.razor` with `@page "/widgets/new"` and `@page "/widgets/{Id:guid}"`:
   - `<CslaForm T="WidgetEdit" Load="Load" Saved="..." Deleted="..." Closed="..." Context="form">` with one field per property and the Save, Cancel and Delete buttons.
   - `Load` returns `Portal.FetchAsync(id)` or `Portal.CreateAsync()`.
3. **Code only in `.razor.cs`.** `.razor` files hold no `@code` (enforced by tests). Inject `Csla.IDataPortal<T>` by its full name: `Csla` and Blazor both define `[Inject]`.
4. **No styling** until the UI phase.
5. **Tests:**
   - a bUnit test in `Components.Tests/Pages` for what the page shows per role;
   - a route in `Integration.Tests/SamplePagesTests` (served signed in, challenged anonymously).
6. **Labels:** add `Labels` keys named `{EntityType}_{Property}` for every audited property, so the history shows friendly names.
