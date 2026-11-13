# Plan 5: Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable `Csla*` component family. Every component inherits `CslaBaseComponent`, and every behaviour in spec §12.3 is wired. GCDS interop runs under the strict CSP. Unstyled sample pages exercise all of it, and the code-behind APIs are what the UI phase builds on.

**Architecture:**
- **Components** references Core, Business and Csla.Blazor, never Data or Infrastructure. What it needs from the server comes through Core contracts (hub, presence, drafts, current user, security events) or the CSLA data portal (history).
- **Code layout:** every component is `X.razor` (one bare bound element) plus `X.razor.cs` (all code).
- **Pure logic is kept out of components** so it can be unit-tested without a renderer: error classification, field diffs, conflict merge, rule hints and the debounce.
- **Tests:** bUnit covers the components; pages and endpoints are covered by `WebAppFactory`.

**Tech Stack:** Csla.Blazor 10.2.0, bUnit 2.15.2, GC Design System 1.7.0 (`@cdssnc/gcds-components`, self-hosted), Blazor JS initializers.

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md` §12 (all of it), and also:
- §10 language switch with draft flush;
- §11 component behaviour, conflict dialog, presence and drafts;
- §9 session timeout;
- §9.1 losing access while an editor is open;
- §13 sample features.

## Global Constraints

- **Project references:** Components may reference only Core, Business and `Csla.Blazor`. This is enforced by `ProjectGraphTests`.
- **Razor files:** every component has a `.razor` and a `.razor.cs`. The `.razor` holds only the bare element(s): no layout, no styling, no `class` bound on a GCDS host.
- **One subscription per form or table.** Fields never subscribe.
- **CSLA is the only validator.** GCDS fields get `validate-on="other"`, and forms get `novalidate`.
- **Field attributes come from the object:** `label` from `FriendlyName`, `error-message` from `ErrorText`, `hint` from the parameter or the warning/information text, and `required`/`maxlength` from the rules (`AppBusinessBase.PropertyHasRule`/`MaxLengthOf`), never from markup.
- **GCDS specifics:**
  - Object props (`options`, `error-links`) are JSON strings.
  - A no-write field drops `required`. It is `readonly` on `gcds-input` and rendered as text elsewhere.
  - Every field host has a stable `id`.
- **Custom events:** `gcdsinput`, `gcdschange`, `gcdsblur`, `gcdsfocus`, `gcdsclick`, registered in `CslaStarter.Components.lib.module.js` (`afterWebStarted`) and declared with `[EventHandler]` in `EventHandlers`.
- **CSP:** no inline script or style. The JS initializer passes the page's `csp-nonce` meta value to the GCDS (Stencil) loader's `setNonce` before defining the elements.
- **Timings:**
  - list refresh debounce: 500 ms;
  - draft autosave: 2 s after the last edit, while dirty;
  - clean-editor refetch: on notice;
  - session timeout from `SessionOptions` (warning at 18 min, sign-out at 20 min).
- **Messages:** every user-facing text is a `Messages` resource in English and French, and every screen-reader announcement goes through `ILiveAnnouncer`.
- **Telemetry:** an `ActivitySource "CslaStarter.Components"`; spans `load`, `save`, `delete` and `refresh`, tagged `component` and `entity.type`.

## Review Focus

1. **A clean editor that receives another user's change refetches, highlights the changed fields and announces "Updated by Jane".** A dirty editor never loses the user's edits; it shows the banner. *Pinned by* `Form_CleanEditor_RefetchesAndAnnounces` and `Form_DirtyEditor_ShowsBannerAndKeepsEdits` (Task 4).
2. **Saving after another user saved opens the conflict dialog.** "Apply mine" re-applies only the fields I changed, including child adds, edits and deletes matched by key, and never saves by itself. *Pinned by* `ConflictMerge_AppliesOnlyMyChanges_ChildrenByKey` and `Form_ConcurrencyOnSave_OpensConflictDialog` (Task 4).
3. **An editor whose record became hidden (member removed) closes with a message, and its draft is discarded.** *Pinned by* `Form_AccessLost_ClosesAndDiscardsDraft` (Task 4).
4. **Switching language or timing out with unsaved changes keeps them:** the draft is flushed first and offered back after the reload. *Pinned by* `LanguageToggle_FlushesDraftsBeforeNavigating` (Task 8) and `Form_OpenWithDraft_OffersRestore_WarnsWhenRecordChanged` (Task 5).
5. **A masked sensitive value never reaches the browser until revealed.** Revealing it requires a role and writes a security event. *Pinned by* `SensitiveField_RendersMask_RevealLogsEvent` (Task 8).

---

### Task 1: Base component, services and test project

**Files:**
- **Create in Components:**
  - `Base/CslaBaseComponent.cs`
  - `Services/IToastService.cs`, `Services/ToastService.cs`
  - `Services/ILiveAnnouncer.cs`, `Services/LiveAnnouncer.cs`
  - `Services/CircuitOrigin.cs`
  - `Telemetry/ComponentTelemetry.cs`
  - `ComponentsServiceCollectionExtensions.cs`
- **Create in Business:** `Errors/BusinessErrors.cs`, which classifies exceptions so Components never touch Dal types.
- **Create test project** `tests/CslaStarter.Components.Tests/`:
  - csproj with bUnit, xunit.v3 and Csla.Testing
  - `CLAUDE.md`
  - `ComponentTestContext.cs`: a bUnit context with CSLA, fake DALs, a test principal and fake hub/presence/drafts.
- **Modify:**
  - Components csproj: add Csla.Blazor.
  - `Directory.Packages.props` (bunit already pinned).
  - solution, `ProjectGraphTests`, `docs/claude/testing.md`.
- **Tests:** `Base/BaseComponentTests.cs`, `Business.Tests/Errors/BusinessErrorsTests.cs`.

**Interfaces (Produces):**

```csharp
public enum BusinessErrorKind { AccessDenied, Conflict, NotFound, Invalid, Cancelled, Unexpected }
public sealed record BusinessError(BusinessErrorKind Kind, string Message, Exception Exception);   // Message localized
public static class BusinessErrors { BusinessError Classify(Exception exception); }               // unwraps DataPortalException / AggregateException

public abstract class CslaBaseComponent : ComponentBase, IAsyncDisposable
{
    protected string ComponentId { get; }                       // "csla-<n>", stable per instance
    protected CancellationToken DisposalToken { get; }
    protected bool IsBusy { get; }
    [Inject] protected ICurrentUser CurrentUser { get; set; }
    [Inject] protected IToastService Toasts { get; set; }
    [Inject] protected ILiveAnnouncer Announcer { get; set; }
    protected IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler);   // marshals with InvokeAsync, skips own OriginId, disposed with the component
    protected IDisposable OnResync(Func<Task> handler);
    protected Task<bool> RunAsync(string operation, Func<CancellationToken, Task> work);  // busy, span, classify → toast/announce; returns success
    protected virtual Task OnErrorAsync(BusinessError error);  // default: toast; AccessDenied also writes SecurityEvent(AccessDenied)
    protected virtual ValueTask DisposeAsyncCore();
}
public interface IToastService { IReadOnlyList<Toast> Toasts { get; } event Action? Changed; void Show(ToastKind kind, string message); void Dismiss(Guid id); }
public interface ILiveAnnouncer { event Action<string, bool>? Announced; void Announce(string message, bool assertive = false); }
public sealed class CircuitOrigin { string Id { get; } }        // scoped (one per circuit); stamped into ApplicationContext.ClientContext[CslaDataAccessContext.OriginKey]
public static IServiceCollection AddCslaStarterComponents(this IServiceCollection services);
```

- [ ] **Step 1: Write the failing tests.**
  - `BusinessErrors_UnwrapsDataPortalException_ToEachKind` (a theory over Security, Dal Concurrency, Dal NotFound, ValidationException, OperationCanceled and others).
  - `Base_RunAsync_SetsBusyAndClearsIt`.
  - `Base_RunAsync_AccessDenied_ShowsToastAndWritesSecurityEvent`.
  - `Base_RunAsync_Unexpected_ShowsReference`: the message contains the trace id.
  - `Base_Subscribe_IgnoresOwnOrigin_AndMarshalsToRenderer`.
  - `Base_Dispose_RemovesSubscriptions_AndCancelsToken`.
  - `Base_ComponentId_IsStable`.
- [ ] **Step 2: Run the tests.** Expected: FAIL (the types are missing).
- [ ] **Step 3: Implement.**
  - **Origin stamping:** `CircuitOrigin` is stamped into `ClientContext` by `AddCslaStarterComponents` through a scoped `CircuitHandler`, so every data portal call made by the circuit carries it.
  - **Messages:** add `Error_Unexpected` ("Something went wrong. Reference: {0}"), `Error_NotFound`, `Error_Conflict`, `Error_AccessDenied` and `Toast_Saved` in both languages.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add the base component, notification services and component tests`.

### Task 2: `AppViewModel<T>`

**Files:** `Components/Forms/AppViewModel.cs`; test `Forms/AppViewModelTests.cs`.

**Interfaces (Produces):**

```csharp
public class AppViewModel<T> : Csla.Blazor.ViewModel<T> where T : class, ISavable, IEntityIdentity
{
    public AppViewModel(ApplicationContext applicationContext);      // ManageObjectLifetime = true
    public bool IsDirty { get; }  public bool IsSavable { get; }
    public bool CanEdit { get; }  public bool CanDelete { get; }  public bool CanCreate { get; }   // per object; RefreshPermissions() recomputes
    public event Action? BusyChanged;                                  // raised when Model or any property's busy state changes
    public Task CancelAsync();                                         // undo to the original values
    public void RefreshPermissions();
}
```

- [ ] **Step 1: Write the failing tests.**
  - `ViewModel_Cancel_RestoresOriginalValues`, including a removed task coming back.
  - `ViewModel_BusyChanged_FiresWhileAsyncRuleRuns`.
  - `ViewModel_Permissions_PerObject`: Client false, Technical edit but not delete, Admin all.
  - `ViewModel_IsSavable_FalseWhileBusyOrInvalid`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add AppViewModel with undo, busy events and per-object permissions`.

### Task 3: Form and fields

**Files:**
- **Create in `Components/Forms/`:** `CslaForm.razor(.cs)`, `CslaFormContext.cs`, `FieldHints.cs` (pure: required, maxlength, hint, error from an `IPropertyInfo` and the object's rules).
- **Create in `Components/Fields/`:**
  - `CslaFieldBase.cs`
  - `CslaTextField`, `CslaTextArea`, `CslaNumberField`, `CslaDateField`
  - `CslaSelect`, `CslaCheckbox`, `CslaCheckboxGroup`, `CslaRadioGroup`
  - each as `.razor` + `.razor.cs`
- **Create:** `Components/Interop/EventHandlers.cs` and `wwwroot/CslaStarter.Components.lib.module.js` (custom events only; GCDS loading comes in Task 9).
- **Tests:** `Fields/FieldHintsTests.cs`, `Fields/FieldTests.cs`.

**Interfaces (Produces):**

```csharp
public sealed class CslaFormContext<T> { AppViewModel<T> ViewModel; T Model; EditContext EditContext; string FieldId(string property); bool IsChanged(string property); bool IsHighlighted(string property); }
public abstract class CslaFieldBase<TValue> : CslaBaseComponent
{
    [Parameter, EditorRequired] public Expression<Func<TValue>> For { get; set; }
    [Parameter] public string? Hint { get; set; }
    [CascadingParameter] protected ICslaFormContext? Form { get; set; }
    protected Csla.Blazor.IPropertyInfo Property { get; }   // resolved once from For
    protected string FieldId { get; }  protected bool IsRequired { get; }  protected int? MaxLength { get; }
    protected string? ErrorMessage { get; }  protected string? HintText { get; }  protected bool IsReadOnly { get; }
    protected bool IsChecking { get; }                       // async rule busy → "Checking…"
}
public partial class CslaForm<T> : CslaBaseComponent<T>
{
    [Parameter, EditorRequired] public Func<Task<T>> Load { get; set; }
    [Parameter] public RenderFragment<CslaFormContext<T>>? ChildContent { get; set; }
    [Parameter] public EventCallback<T> Saved { get; set; }
    public Task<bool> SaveAsync();  public Task CancelAsync();   // used by CslaButton and shortcuts
}
```

- [ ] **Step 1: Write the failing tests.**
  - `FieldHints_RequiredAndMaxLength_ComeFromRules`, using `ProjectEdit.Code` (Required, MaxLength 20).
  - `FieldHints_WarningAndInfo_BecomeTheHint`.
  - `TextField_RendersGcdsAttributes`: `label`, `required`, `maxlength=20`, `validate-on="other"`, `error-message` after an invalid value, `id`.
  - `TextField_NoWrite_IsReadOnlyWithoutRequired`: Technical on `Budget`.
  - `TextField_AsyncRule_ShowsChecking`.
  - `Select_TypeList_RendersOptionsJson`, using `ProjectStatusTypeList` in the current language.
  - `Field_Changed_ShowsMarker`.
  - `Form_SetsNovalidate`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add CslaForm and the GCDS field components`.

### Task 4: Live updates, presence, access loss and conflicts in the form

**Files:**
- **Create in Components:**
  - `Forms/FieldDiff.cs` (pure: which registered properties differ between two copies, root first, then children matched by key)
  - `Forms/ConflictMerge.cs` (pure: apply my changes, relative to the original, onto the fresh copy)
  - `Live/CslaChangeBanner.razor(.cs)`
  - `Live/CslaPresenceIndicator.razor(.cs)`
  - `Dialogs/CslaConflictDialog.razor(.cs)`
- **Modify:** `CslaForm`.
- **Tests:** `Forms/FieldDiffTests.cs`, `Forms/ConflictMergeTests.cs`, `Forms/FormLiveTests.cs`.

**Behaviour:**
- **Subscriptions and presence:** the form subscribes to `ForRecord(EntityType, EntityKey)` and to `ForRecord(UserAccess, me)`, and joins presence as `Viewing`, switching to `Editing` when dirty.
- **A notice from someone else:**
  - Clean editor: refetch, then `FieldDiff`, highlight the changed fields for 5 s, and announce `Messages.Live_UpdatedBy`.
  - Dirty editor: show the banner, with "Reload" (discards my edits) or "Keep mine".
  - Deleted record: a banner saying the record was deleted, and saving is disabled.
- **Resync:** the editor compares versions by refetching quietly, and if the version changed it behaves as if it had received a notice.
- **Access notice:**
  - It re-fetches.
  - `NotFound` closes the form: it raises `Closed`, shows the `Messages.Live_AccessLost` toast and deletes the draft.
- **`ConcurrencyException` on save:** fetch a fresh copy, then open `CslaConflictDialog` with the `FieldDiff` of theirs against mine.
  - "Use theirs" discards my edits.
  - "Apply mine" runs `ConflictMerge` with my original, my current and their fresh copy. Where both of us changed the same field, the dialog asks which value to keep. The result goes back into the form, unsaved.

**Interfaces (Produces):**

```csharp
public sealed record FieldDifference(string Path, string Property, string FriendlyName, object? Theirs, object? Mine);  // Path "" for root, "Tasks[<key>]" for a child
public static class FieldDiff { IReadOnlyList<FieldDifference> Between(object theirs, object mine); }
public static class ConflictMerge { MergeResult Apply(object original, object mine, object theirs); }   // MergeResult(object Merged, IReadOnlyList<FieldDifference> BothChanged)
```

- [ ] **Step 1: Write the failing tests.**
  - `FieldDiff_RootAndChildrenByKey`.
  - `ConflictMerge_AppliesOnlyMyChanges_ChildrenByKey`.
  - `ConflictMerge_BothChanged_IsReported`.
  - `Form_CleanEditor_RefetchesAndAnnounces`.
  - `Form_DirtyEditor_ShowsBannerAndKeepsEdits`.
  - `Form_OwnSave_IsIgnored`.
  - `Form_Resync_RefetchesWhenVersionChanged`.
  - `Form_AccessLost_ClosesAndDiscardsDraft`.
  - `Form_ConcurrencyOnSave_OpensConflictDialog`.
  - `Form_Presence_JoinsAndSwitchesToEditing`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** `FieldDiff` and `ConflictMerge` work on CSLA's registered properties through `FieldManager.GetRegisteredProperties()`, reading values with `ReadProperty` through `Csla.Reflection.MethodCaller`. Children are matched by `IEntityIdentity.EntityKey`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add live updates, presence, access loss and conflict handling to forms`.

### Task 5: Drafts in the form

**Files:**
- **Create in Components:**
  - `Drafts/DraftAutosave.cs` (debounce on `TimeProvider`)
  - `Drafts/IDraftFlusher.cs` and `Drafts/DraftFlushRegistry.cs` (scoped: every open form registers; `FlushAllAsync` before a language switch or sign-out)
  - `Dialogs/CslaDraftPrompt.razor(.cs)`
- **Modify:** `CslaForm`.
- **Tests:** `Drafts/DraftAutosaveTests.cs`, `Forms/FormDraftTests.cs`.

**Behaviour:**
- **Saving the draft:** while dirty, the draft is saved 2 s after the last edit (`DraftCodec` + `IDraftStore`, key = me + EntityType + EntityKey, version = Model.Version).
- **Deleting the draft:** on save, cancel, "Reload" and "Use theirs".
- **Opening an editor that has a draft:** prompt "Restore unsaved changes from {time}?". If the draft's version differs from the loaded one, add "This record changed since; review before saving." Restoring replaces the model, which stays dirty; discarding deletes the draft.

- [ ] **Step 1: Write the failing tests.**
  - `Autosave_DebouncesTo2SecondsAfterLastEdit`.
  - `Autosave_NotWhenClean`.
  - `Form_OpenWithDraft_OffersRestore_WarnsWhenRecordChanged`.
  - `Form_Save_DeletesDraft`.
  - `FlushRegistry_FlushesEveryDirtyForm`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Autosave and restore drafts in forms`.

### Task 6: Tables

**Files:**
- **Create in `Components/Tables/`:** `CslaTable.razor(.cs)`, `CslaTableColumn.razor(.cs)`, `CslaTableRow.razor(.cs)`, `TableState.cs` (page, page size, sort, descending, filter text, selected key).
- **Tests:** `Tables/TableTests.cs`.

**Interfaces (Produces):**

```csharp
public partial class CslaTable<TList, TItem> : CslaBaseComponent
    where TList : IEnumerable<TItem> where TItem : IEntityIdentity
{
    [Parameter, EditorRequired] public string EntityType { get; set; }           // type topic
    [Parameter, EditorRequired] public Func<TableState, Task<TList>> Load { get; set; }
    [Parameter] public Func<TList, int>? TotalCount { get; set; }               // default: (list as IPagedList)?.TotalCount
    [Parameter] public RenderFragment? Columns { get; set; }
    [Parameter] public EventCallback<TItem> RowSelected { get; set; }
    public TableState State { get; }
}
```

- [ ] **Step 1: Write the failing tests.**
  - `Table_LoadsFirstPage_RendersSemanticTableAndPagination`.
  - `Table_Sort_ReloadsWithState`.
  - `Table_TypeNotice_RefreshesOnceAfter500ms_KeepingPageSortAndSelection`.
  - `Table_Resync_Refreshes`.
  - `Table_HiddenColumn_WhenPropertyNotReadable`: the Budget column for a Client.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** Use a `<table>` with `<th scope="col">` and `aria-sort`, plus `gcds-pagination` (`display="list"`, total pages, current page) through a `gcdsclick` handler.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add CslaTable with server paging, sorting and live refresh`.

### Task 7: Buttons, dialogs, notifications and history

**Files:**
- **Create in Components:**
  - `Buttons/CslaButton.razor(.cs)`
  - `Dialogs/CslaConfirmDialog.razor(.cs)`
  - `Notifications/CslaErrorSummary.razor(.cs)`, `CslaToastHost.razor(.cs)`, `CslaLiveRegion.razor(.cs)`
  - `Security/CslaAuthorize.razor(.cs)`
  - `History/CslaHistory.razor(.cs)`
- **Create in Business:** `Auditing/AuditHistory.cs` (a `ReadOnlyBase` fetched through `IAuditHistoryReader`, so record access applies) and `Auditing/AuditHistoryEntry.cs` (type codes resolved to descriptions in the current language).
- **Tests:** `Buttons/ButtonTests.cs`, `Notifications/ErrorSummaryTests.cs`, `History/HistoryTests.cs`, `Business.Tests/Auditing/AuditHistoryTests.cs`.

**Behaviour:**
- **CslaButton:** `Kind` is `Save`, `Cancel`, `Delete`, `New` or `Custom`.
  - It is disabled while busy and follows `AppViewModel` permissions plus `IsSavable` (Save).
  - Delete asks `CslaConfirmDialog` first.
  - It renders `gcds-button` with `type="button"`.
- **Error summary:** after a failed save, `CslaErrorSummary` renders `gcds-error-summary` with JSON `error-links` (`#fieldId` → message) and takes focus.
- **History:** `CslaHistory` takes type, key and an optional property and shows a paged timeline. Masked values show `Messages.History_Masked`, and type codes show their descriptions. A field's history icon opens it filtered to that property.

- [ ] **Step 1: Write the failing tests.**
  - `Button_Save_DisabledWhenNotSavableOrBusy`.
  - `Button_Delete_HiddenWithoutPermission_ConfirmsFirst`.
  - `ErrorSummary_ListsErrorsWithFieldLinks_AndTakesFocus`.
  - `AuditHistory_HiddenRecord_IsNotFound`.
  - `AuditHistory_TypeCodesShowDescriptions`.
  - `History_RendersEntries_MaskedValuesHidden`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add buttons, dialogs, notifications and the history view`.

### Task 8: Session timeout, language toggle, shortcuts, unsaved-changes guard and sensitive fields

**Files:**
- **Create in Components:**
  - `Session/CslaSessionTimeout.razor(.cs)`, with JS activity reporting (keyboard and pointer, throttled to one call every 30 s)
  - `Localization/CslaLanguageToggle.razor(.cs)`
  - `Forms/FormShortcuts.cs` (Ctrl+S and Esc through `wwwroot/shortcuts.js`; Esc is ignored inside open dialogs)
  - `Forms/UnsavedChangesGuard` (`NavigationLock` + `CslaConfirmDialog`; `beforeunload` while dirty)
  - `Fields/CslaSensitiveField.razor(.cs)`
- **Create in Core:** `Security/SensitiveAttribute.cs` (`RevealRoles`).
- **Create in Web:** the `/account/signout` endpoint (POST, antiforgery; signs out of the cookie, then Entra or MockOidc).
- **Modify:** `UserEdit.EmployeeNumber` gets `[Sensitive(RevealRoles = [UserRoleType.Admin])]`.
- **Tests:** `Session/SessionTimeoutTests.cs`, `Localization/LanguageToggleTests.cs`, `Fields/SensitiveFieldTests.cs`, `Integration.Tests/SignOutTests.cs`.

**Behaviour:**
- **Session timeout:** `CslaSessionTimeout` reads `SessionTimeoutMonitor` through a Core contract (`ISessionActivity`, with `State`, `Remaining` and `StateChanged`) that Infrastructure registers.
  - At Warning it opens a dialog with "Stay signed in" (records activity) and announces it.
  - At Expired it flushes drafts, then posts to `/account/signout`.
- **Language toggle:** `CslaLanguageToggle` calls `DraftFlushRegistry.FlushAllAsync()`, then navigates to `/culture/set?culture=…&redirectUri=<current>` with `forceLoad`.
- **Sensitive fields:** `CslaSensitiveField` renders `••••` server-side. "Reveal" is available only with `RevealRoles`; it writes `SecurityEvent(SensitiveReveal)` and then shows the value.

- [ ] **Step 1: Write the failing tests.**
  - `SessionTimeout_Warning_OpensDialogAndAnnounces`.
  - `SessionTimeout_Expired_FlushesDraftsThenSignsOut`.
  - `LanguageToggle_FlushesDraftsBeforeNavigating`.
  - `SensitiveField_RendersMask_RevealLogsEvent`.
  - `SensitiveField_NoRevealRole_NoButton`.
  - `Shortcuts_CtrlS_Saves_EscCancels`, through the form API invoked by JS interop.
  - `SignOut_RequiresAntiforgery_AndClearsCookie`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add session timeout, language toggle, shortcuts, unsaved-changes guard and sensitive fields`.

### Task 9: GC Design System assets under the strict CSP

**Files:**
- `scripts/update-gcds.sh`: pins `@cdssnc/gcds-components@1.7.0` and `@cdssnc/gcds-utility`, fetches from the npm registry, checks the tarball `integrity` (sha512) against values pinned in the script, and copies `dist/gcds` into `src/CslaStarter.Components/wwwroot/gcds/`.
- The committed assets.
- The JS initializer loads `gcds.esm.js` (module), calls `setNonce(meta[csp-nonce])`, then `defineCustomElements()`. `App.razor` links `gcds.css`.
- **Tests:**
  - `Architecture.Tests/GcdsAssetsTests.cs`: the assets are present and the version matches the pin.
  - `Integration.Tests/WebHostTests.Csp_AllowsGcdsAssets`: the assets are served, and the CSP has no `unsafe-inline`.

- [ ] **Step 1: Write the failing tests.**
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - **Before fetching:** say in the ledger what is fetched and from where.
  - **If Stencil injects a style without a nonce:** add that `sha256-` hash to `style-src` (computed by the test, not by hand) rather than `unsafe-inline`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Self-host the GC Design System under the strict CSP`.

### Task 10: Unstyled sample pages

**Files:**
- **Create in `Web/Components/Pages/Sample/`:**
  - `Users/UserListPage`, `Users/UserEditPage`
  - `Profile/MyProfilePage`
  - `Projects/ProjectListPage`, `Projects/ProjectEditPage` (tasks, members, close, history)
  - each as `.razor` + `.razor.cs`
- **Create:** `Web/Components/Layout/AppShell` code with the data the GCDS shell needs: language href and account slot.
- **Modify:** `Program.cs` (`AddCslaStarterComponents`, sign-out endpoint), `Routes.razor`.
- **Test:** `Integration.Tests/SamplePagesTests.cs`. With `WebAppFactory` and the test auth handler it checks:
  - every page answers 200 for the roles allowed and access-denied otherwise;
  - Projects for a Client lists only member projects;
  - the edit page of a hidden project shows not found.

- [ ] **Step 1: Write the failing tests.**
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** Pages are compositions of the components with no styling. The routes are:
  - `/users`, `/users/{id:guid}`
  - `/profile`
  - `/projects`, `/projects/new`, `/projects/{id:guid}`
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add unstyled sample pages`.

### Task 11: Documentation

**Files:**
- **Create:** `docs/claude/components.md` (the hierarchy, the base contract, writing a form, page or table, GCDS traps, testing with bUnit) and `docs/claude/recipes/add-a-page.md`.
- **Update:**
  - root `CLAUDE.md` guides list
  - `src/CslaStarter.Components/CLAUDE.md`, `src/CslaStarter.Web/CLAUDE.md`
  - `tests/CslaStarter.Components.Tests/CLAUDE.md`
  - `docs/claude/testing.md`

- [ ] **Step 1: Write the guides from the code.**
- [ ] **Step 2: Run `dotnet test`.** Expected: all pass, including `ClaudeDocsTests` (links and per-project `CLAUDE.md`).
- [ ] **Step 3: Commit** with the message `DEV-38 Document the components and add the add-a-page recipe`.
