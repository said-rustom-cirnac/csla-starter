# CSLA Starter — Technical Design

- **Date:** 2026-11-12
- **Status:** Draft, for approval
- **Owner:** Said Rustom
- **Companion:** `docs/architecture/csla-starter-architecture.html` (the short, visual version for the architecture director)

Decisions marked **(Said)** came from Said. Decisions marked **(Claude's call)** were made during design and can be overturned during review.

---

## 1. Intent

This is a reusable starter template for Government of Canada line-of-business web applications. It is built on .NET 10, Blazor Server and CSLA 10.2.

Every new app built from it should start out:

- **Secure:** a Protected B baseline from day one.
- **Bilingual:** English and French.
- **Audited:** every change is recorded.
- **Live:** open screens learn about other users' changes.

CSLA business objects are the single source of truth for validation and authorization. Reusable Blazor components display those rules without anyone writing them twice.

The template also has to be easy for Claude to work in, through documentation that always stays in sync with the code. It must be good enough to pass review by the architecture director.

## 2. Goals, non-goals, anti-goals

**Goals**

1. A `dotnet new` template that produces a running, tested and secured app.
2. CSLA business layer conventions: property registrations, then properties, then business rules, then data portal operations. It includes editable roots, child lists, read-only lists, commands and `*Type` lookup classes.
3. Reusable components (`.razor` + `.razor.cs`) that all inherit one `CslaBaseComponent`. They provide notifications, live updates, busy state, unsaved-changes protection, property-level authorization, accessibility and the other behaviours listed in §12.
4. Every data change is audited with its old and new values, and a History component shows those changes.
5. Live updates go only to the components that have the changed record open, so they stay cheap.
6. en/fr throughout: labels come from `[LocalizedDisplayName]`, and rule messages, type descriptions and errors are all translated.
7. Runs on on-prem IIS or Azure App Service, on one instance or several.
8. A complete testing story, including a fake Entra ID, so nobody needs a real tenant.
9. Root and per-project `CLAUDE.md` files that point to detailed guides, kept up to date.

**Non-goals (this phase)**

- Visual design and page layouts. The UI phase comes later with GC Design System and Impeccable. This phase writes the component code and only minimal `.razor` markup.
- Multi-tenancy, a public REST API, mobile apps, WebAssembly/Auto render modes.
- Admin screens for editing `*Type` values. Type values are owned by code.
- Document or file storage.

**Anti-goals (it fails if…)**

- Validation or permission logic is duplicated in the UI.
- A change notification costs work for components that don't show the changed record.
- Any page or field can show English text in French mode.
- Business objects or Protected B data are sent to the browser beyond what is rendered.
- Claude files (`.claude/`, `CLAUDE.md`, `docs/claude/`, `docs/superpowers/`, `.impeccable/`) appear on `main`.
- The template carries demo shortcuts (secrets in config, disabled security, `TODO` auth) that an app could ship by accident.

## 3. Constraints

| Item | Value |
|---|---|
| Runtime | .NET 10 (LTS, supported to Nov 2028). Microsoft.* packages pinned to 10.0.x (latest 10.0.15). .NET 11 is out; don't take 11.x packages. |
| SDK | 10.0.115 installed; `global.json` pins the 10.0.1xx band |
| CSLA | 10.2.0: `Csla`, `Csla.AspNetCore`, `Csla.Blazor`, and `Csla.Testing` for tests |
| UI host | Blazor Web App, global Interactive Server render mode **(Said: Blazor Server)** |
| Database | PostgreSQL (tests use the `postgres:18` image) **(Said)** |
| ORM | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 |
| Identity | Microsoft Entra ID **(Said)** |
| UI kit (later) | GC Design System `@gcds-core/components` 1.7.0, self-hosted **(Said)** |
| Hosting | On-prem IIS or Azure App Service **(Said)** |
| Security | Protected B baseline, aligned to ITSG-33 **(Said)** |
| Accessibility | WCAG 2.1 AA (EN 301 549) |
| Languages | en-CA, fr-CA (Official Languages Act) |

## 4. Decision register

| # | Topic | Decision | By |
|---|---|---|---|
| D1 | Sign-in | Entra ID via Microsoft.Identity.Web. Users are keyed on `tid`+`oid`. MFA comes from Entra Conditional Access. | Said |
| D2 | Roles | Stored in our DB, many per user. Added as role claims by `IClaimsTransformation`. Checked with `HasRole(UserRoleType.Admin)`. | Said |
| D3 | Data access | DAL interfaces (`[Inject] IXxxDal`) with an EF Core implementation. The data portal runs in-process. | Said |
| D4 | Audit | Every change is captured with old and new values, in append-only tables, plus a History component. | Said |
| D5 | Live updates | Lists refresh quietly. A clean editor auto-reloads. A dirty editor shows a banner. Optimistic concurrency protects saves. | Said |
| D6 | Cross-server messages | PostgreSQL LISTEN/NOTIFY backplane behind `IEntityChangeNotifier`. In-memory when there's one instance. No Redis. | Claude's call |
| D7 | Types | `*Type : CslaType<T>` with `public const int` codes, seeded by migrations, cached, with IsActive and SortOrder. | Said (default) |
| D8 | Labels | `[LocalizedDisplayName(nameof(Labels.Key))]`, a subclass of `DisplayNameAttribute` that reads `.resx` per culture. | Said (default) |
| D9 | UI kit | GC Design System 1.7.0 web components wrapped by our Csla* components. Not WET/GCWeb. | Said |
| D10 | Property permissions | No read → hidden. No write → read-only text. Overridable per component. | Said (default) |
| D11 | Template | `dotnet new csla-blazor -n MyApp` (sourceName `CslaStarter`), packaged from the `claude` branch. The `--claude` option (default `true`) includes the Claude guides and skills; `--claude false` leaves them out. | Said |
| D12 | Sample | User and role management, plus Projects with Tasks and members. | Said (default) |
| D13 | Branches | `DEV-<n>-<slug>` → `claude` → automated sync to `main` with Claude paths stripped, plus a guard on `main`. | Said (default) |
| D14 | Remote/CI | GitHub `said-rustom-cirnac/csla-starter` (public, user-owned, currently empty), with GitHub Actions. | Said |
| D19 | Record-level access | Visibility through EF Core named query filters. Per-record actions through CSLA object rules. Live updates and presence never leak data (§9.1). | Said |
| D15 | Prerendering | Off: `new InteractiveServerRenderMode(prerender: false)`. Avoids double fetches and an unauthenticated first pass. | Claude's call |
| D16 | Drafts | Server-side draft store in PostgreSQL, encrypted with Data Protection. Used for disconnects, language switches and session timeouts. | Claude's call |
| D17 | Logging | OpenTelemetry (traces, metrics, logs) over OTLP. Serilog file sink is opt-in for IIS sites with no collector. | Claude's call |
| D18 | Tests | xUnit v3, Csla.Testing, bUnit, Testcontainers, Playwright with axe, mock-oauth2-server as the fake Entra ID. | Said (default) |

## 5. Solution structure

```
CslaStarter.slnx
global.json · Directory.Build.props · Directory.Packages.props · .editorconfig · nuget.config
docker-compose.yml                 postgres + mock-oauth2-server for local dev (no tenant needed)
.template.config/template.json     dotnet new template (sourceName: CslaStarter)
src/
  CslaStarter.Core            contracts + localization: ICurrentUser, IEntityChangeNotifier/Hub, IPresenceTracker,
                              IDraftStore, IAuditHistoryReader, ISecurityEventLog, IClock, Labels/Messages .resx,
                              LocalizedDisplayNameAttribute, SensitiveAttribute
  CslaStarter.Dal             DAL interfaces, DTOs, DAL exceptions (ConcurrencyException, NotFoundException)
  CslaStarter.Business        CSLA: base classes, CslaType, rules, authorization rules, domain objects
  CslaStarter.Data            EF Core: AppDbContext, entities, configurations, migrations, DAL implementations,
                              audit interceptor, draft store, security event log, Data Protection key store
  CslaStarter.Infrastructure  change hub + PostgreSQL backplane, presence, claims transformation, culture,
                              session timeout circuit handler, telemetry, health checks, DI extensions
  CslaStarter.Components      Razor class library: CslaBaseComponent family, GCDS interop (JS initializer + events)
  CslaStarter.Web             host / composition root: Program.cs, auth, security headers, endpoints, pages
tests/
  CslaStarter.Business.Tests        CslaStarter.Components.Tests     CslaStarter.Integration.Tests
  CslaStarter.E2E.Tests             CslaStarter.Architecture.Tests
docs/architecture/                  director-facing documents (ships to main)
docs/claude/ · docs/superpowers/    Claude-only (claude branch)
```

**Allowed references.** These are enforced by `Architecture.Tests`:

```
Core            → (Microsoft.Extensions.* abstractions only)
Dal             → Core
Business        → Core, Dal, Csla
Data            → Core, Dal, EF Core, Npgsql
Infrastructure  → Core, Data, Npgsql
Components      → Core, Business, Csla.Blazor        (never Data or Infrastructure)
Web             → all                                 (composition root only)
```

**Build settings** in `Directory.Build.props`:

- `Nullable` enable, `TreatWarningsAsErrors`, `AnalysisLevel` latest-recommended.
- `NuGetAudit` with `NuGetAuditMode=all` and the audit level set to fail the build.
- Central Package Management with lock files, and package source mapping.
- Deterministic builds, SourceLink.

## 6. Business layer (CSLA 10.2)

### 6.1 Configuration

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore()                 // mandatory: Csla.Blazor loads context managers from it
    .AddServerSideBlazor()           // in-memory ApplicationContext per circuit (default)
    .DataPortal(dp => { dp.UseLegacyOperationMethods = false; dp.AddServerSideDataPortal(); }));
```

- Don't inject or call `StateManager`. It isn't registered in in-memory mode.
- `UseCslaPermissionsPolicy` is on, so pages can use `[HasPermission(AuthorizationActions.EditObject, typeof(ProjectEdit))]`.
- Don't use the `Csla.Generator.AutoImplementProperties` generator. It hides the registrations and breaks the layout convention.
- The data-portal dispatch generator from 10.1 isn't published on NuGet, so dispatch stays reflection-based.

### 6.2 Class layout convention

This is enforced by a Roslyn syntax test in `Architecture.Tests`. Every business class orders its members like this:

1. **Property registrations:** `public static readonly PropertyInfo<T> XProperty = RegisterProperty<T>(nameof(X));`
2. **Properties:** `[LocalizedDisplayName(...)]`, `GetProperty`/`SetProperty`/`LoadProperty`, with `!` on non-nullable reference types
3. **Business rules:** `AddBusinessRules()`, then `[ObjectAuthorizationRules] AddObjectAuthorizationRules(...)`
4. **Data portal operations:** `[Create]`, `[Fetch]`, `[Insert]`, `[Update]`, `[DeleteSelf]`, `[Delete]`, `[Execute]`, and the child variants

`*Type` classes put their `public const int` codes above step 1.

### 6.3 Base classes (in `Business/Base`)

- **`AppBusinessBase<T>`**
  - Inherits `BusinessBase<T>`; implements `IEntityIdentity` (`EntityType`, `EntityKey`).
  - Holds the audit stamp properties (CreatedOn/By, ModifiedOn/By, read-only) and, on aggregate roots, `Version` (`uint`, the root row's Npgsql `xmin`).
  - **Concurrency is per aggregate (Claude's call):**
    - Every save of an aggregate updates the root row's ModifiedOn/By, even when only children changed, which bumps the root's `xmin`.
    - The root UPDATE checks the version the user loaded, so any change anywhere in the aggregate by another user makes the save fail.
    - Child rows carry no check of their own. A shared DAL helper (`TouchRootAsync`) does this, so no DAL can forget it.
  - Exposes `GetRuleDescriptions()` publicly, which the UI uses for the required and max-length hints.
  - Exposes `PropertyHasRule(propertyName, ruleType)`. It parses rule URIs and caches results per (type, property).
- **The other base types:** `AppReadOnlyBase<T>`, `AppReadOnlyListBase<TList,TItem>`, `AppBusinessListBase<TList,TItem>`, `AppCommandBase<T>`, and `AppNameValueList` where needed.
- **`CslaType<T>`**
  - A `ReadOnlyBase` with `Code` (int), `DescriptionEn`, `DescriptionFr`, `IsActive`, `SortOrder`, and a computed `Description` that follows `CultureInfo.CurrentUICulture`.
  - **`CslaTypeList<TList,T>`** is a read-only list, cached in `ITypeCache` (IMemoryCache). It is invalidated through the change hub.
- **Concrete types:**

```csharp
public sealed class UserRoleType : CslaType<UserRoleType>
{
    public const int Admin = 1;
    public const int Client = 2;
    public const int Technical = 3;
    // ...registrations / fetch inherited
}
```

- **`*Type` storage:** each type maps to its own table (`user_role_type`, `project_status_type`), seeded with `HasData`.
- **Seed consistency:** a test checks that every `const` has a seed row and every seed row has a `const`. A startup health check verifies the database matches.

### 6.4 Rules and authorization

- **Validation:** use the CommonRules (`Required`, `MaxLength`, `MinValue`, `RegExMatch`, …) with their **`Func<string>` message overloads**.
  - Rules are registered once per type per process, so a message string captured at registration time would be stuck in one language.
- **Severities:** use all three (Error, Warning, Information). Async rules derive from `BusinessRuleAsync`; the UI shows "Checking…" while one runs.
- **Role checks:** the `HasRole(action, [property], params int[] roleCodes)` rule checks the role claims for the given codes.
  - CSLA only evaluates the **first** rule per (property, action), so a single rule takes every allowed role.
  - `ICurrentUser.HasRole(int)` uses the same claims, so code inside a business rule can say `if (currentUser.HasRole(UserRoleType.Admin))`.
- **Object-level rules:** `AddObjectAuthorizationRules` registers rules for create, get, edit and delete.
- **CSLA behaviour to rely on:**
  - `SetProperty` throws when write permission is missing, and `GetProperty` returns the default when read permission is missing.
  - Results are cached per object and cleared when the principal reference changes.
- **Data portal:** each data portal call runs in a **new DI scope**, so there's one DbContext per call. It runs off the UI thread, so components marshal back with `InvokeAsync`.
  - The user, culture and origin ID reach the data layer through `ApplicationContext` (principal + `ClientContext`), not through scoped services from the circuit.
- **Saving:** a save returns a new instance (`AutoCloneOnUpdate`). Outside the ViewModel, always write `obj = await obj.SaveAsync()`. The CSLA analyzers enforce this.
- **Field types:** must be types MobileFormatter supports: primitives, string, decimal, DateTime/Offset, DateOnly/TimeOnly, Guid, byte[], enums, and nullable versions. No `List<string>`, dictionaries or POCOs; use child objects instead.
- **NRT:** don't set CSLA properties in constructors; do it in `[Create]`.

### 6.5 `ICurrentUser`

- Scoped. Reads `ApplicationContext.User`.
- Exposes `UserId` (our DB id), `ObjectId` (`oid`), `DisplayName`, `RoleCodes`, `HasRole(int)`, `HasAnyRole(params int[])`, and `PreferredLanguage`.

## 7. Data layer

- **DbContext setup:** one `AppDbContext`, using `UseNpgsql(dataSource)`, `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions 10.0.1), and the `xmin` concurrency token (`[Timestamp] uint Version`).
- **DAL implementations:**
  - Map entities to DTOs.
  - Turn `DbUpdateConcurrencyException` into `Dal.ConcurrencyException`. The UI shows it as "someone saved first".
  - Row lists use paged queries (`Skip`/`Take` with a total count) behind criteria objects.
- **Database roles** (least privilege):
  - `app_owner` runs migrations.
  - `app_runtime` gets DML on app tables, plus **INSERT + SELECT only** on the audit and security-event tables, and no DDL.
  - The app always connects as `app_runtime`.
- **Migrations:** shipped as an EF migration bundle and run by the deployment pipeline as `app_owner`. The app never migrates at startup outside Development.
- **Connections:**
  - TLS with `SslMode=VerifyFull`.
  - On Azure, Entra token auth for PostgreSQL Flexible Server through Npgsql's periodic password provider and managed identity, so there's no DB password.
  - On IIS, the password comes from protected configuration or a secret store, never from `appsettings`.
- **Data Protection keys:** stored in PostgreSQL (`PersistKeysToDbContext`).
  - Encrypted at rest with `ProtectKeysWithCertificate` on IIS, or `ProtectKeysWithAzureKeyVault` on Azure.
  - `SetApplicationName` is fixed, so every instance shares the same keys.

## 8. Audit and history (D4)

- **Capture:**
  - An EF Core `SaveChangesInterceptor` records every Added, Modified and Deleted entity: `audit_event` (id, occurred_at, user_id, user_name, correlation_id, entity_type, entity_key, root_type, root_key, action), plus one `audit_change` row per property (event_id, property, old_value, new_value, is_masked).
  - It is written in the same transaction as the change, so there is no audit gap.
- **Mapping for display:** entity property names match business property names, which a test enforces. Attributes on entities control how values are shown:

| Attribute | Effect |
|---|---|
| `[AuditAs(typeof(ProjectEdit))]` | The business type whose friendly names are shown |
| `[TypeCode(typeof(ProjectStatusType))]` | The value shows as the type description, e.g. "Active" / "Actif" |
| `[AuditMask]` | Stores "changed" without the values |
| `[AuditIgnore]` | Skips technical columns |

- **Protection:**
  - The database grants are insert-only (see §7).
  - A trigger blocks UPDATE and DELETE on the audit tables, even for `app_owner`.
  - pgAudit (supported on Azure Flexible Server) is recommended for DBA sessions. It's documented in the template, not enforced, because it's an ops choice.
- **Reading:**
  - `IAuditHistoryReader.GetAsync(entityType, key, property?, page)` returns entries with friendly names, resolved through CSLA's registered `PropertyInfo` for the current culture.
  - History is only shown when the user can read the object. Properties the user can't read are left out. Masked values never leave the server.
- **Security events:**
  - Recorded in `security_event`: sign-in, sign-out, access denied, role changes, session timeouts and masked-field reveals.
  - Also emitted as structured OTel logs, so they can be forwarded to a SIEM.
- **Retention (Claude's call):** audit data is kept indefinitely, in plain tables (no partitioning).
  - Removing old audit data is a documented, controlled DBA procedure: a change ticket, disabling the trigger inside one transaction, with pgAudit recording the session.
  - An app's own retention and disposition schedule is out of scope.

## 9. Identity and security

- **Sign-in:**
  - `AddMicrosoftIdentityWebApp` with an auth cookie configured as `__Host-` prefix, Secure, HttpOnly, SameSite=Lax, an 8-hour absolute lifetime and no sliding expiration.
  - The fallback policy is `RequireAuthenticatedUser` (deny by default).
- **Roles:**
  - `IClaimsTransformation` must be idempotent. It caches the DB role lookup for 5 minutes, and the cache is invalidated through the change hub when a role assignment changes.
  - It adds `ClaimTypes.Role` claims, one per role code, and the identity's `RoleClaimType` must match.
  - A user who signs in for the first time is created with **no roles**; an admin then assigns them.
  - **First Admin (Claude's call):** the deployer runs `CslaStarter.Web bootstrap-admin --tenant <tid> --object-id <oid>` once, as `app_owner`.
    - It refuses if an Admin already exists.
    - It writes a security event.
    - It's the only way to create an Admin outside the UI. There's no "first user becomes Admin" and no admin ID in config.
    - docker-compose and the tests seed their users with the same command.
- **Mid-session revalidation:** `RevalidatingServerAuthenticationStateProvider` revalidates every 10 minutes, and immediately when that user's roles change (the hub pushes the change), so open circuits pick up new permissions or sign out disabled users.
- **Test identity:** in the Test and Development environments, a plain `AddOpenIdConnect` points at mock-oauth2-server, with `RequireHttpsMetadata=false` and `MapInboundClaims=false`. It feeds the same claims transformation. It is selected by configuration, and the app **refuses to start** in Production if the mock authority is configured.
- **Session timeout (ITSG-33 AC-11/AC-12):**
  - A `CircuitHandler` tracks inbound activity.
  - After 18 idle minutes, a warning dialog counts down 2 minutes with a "Stay signed in" button.
  - At 20 minutes, drafts are flushed, then the app signs out with a force-reload. Both times are configurable.
- **Headers:**
  - HSTS and HTTPS redirection.
  - CSP sent as a header: `default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'nonce-…'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; form-action 'self' https://login.microsoftonline.com; upgrade-insecure-requests`.
    - The nonce is generated per request.
    - `<meta name="csp-nonce">` lets GCDS's Stencil runtime apply the nonce to the `<style>` it injects.
    - `ContentSecurityFrameAncestorsPolicy` is set to `'none'`.
  - Also `X-Content-Type-Options`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` (deny-all baseline), and `Cross-Origin-Opener-Policy`.
- **Blazor Server hardening:**
  - `DetailedErrors` is off outside Development.
  - `MaximumReceiveMessageSize` stays at its 32 KB default, and `MaximumParallelInvocationsPerClient` stays at 1.
  - `DisconnectedCircuitMaxRetained` and the retention period are tuned in config.
  - All JS interop results are validated, and `MarkupString` is never used with user data (enforced by a test).
- **Other protections:**
  - Antiforgery comes from `UseAntiforgery` after auth. Plain forms (culture switch, sign-out) carry `<AntiforgeryToken/>`.
  - The rate limiter covers the culture and sign-out endpoints.
  - Errors show users a bilingual message plus a reference number (the trace ID), never a stack trace.
  - Logs carry no PII: values are redacted, and IDs are used instead of names where possible.
- **Supply chain:**
  - NuGet audit, CodeQL, Dependabot, and an SBOM per release (CycloneDX). All are free because the repository is public. Secret scanning and push protection are already on.
  - GCDS assets are pinned to 1.7.0 and self-hosted. The `gcds.css` Google Fonts `@import` is replaced with self-hosted `@gcds-core/fonts` 1.1.1.

### 9.1 Record-level access (Said: include)

**Why rules alone aren't enough.**

- A CSLA authorization rule decides about **one** object: get (with criteria), or edit and delete (with the instance). It can't remove rows from a list.
- Filtering a list after loading it would pull hidden records out of the database, and it would break paging and counts: a page of 25 showing 3 rows, totals that reveal hidden records.
- So rows are filtered where they're queried, and rules handle per-record actions.

**Pattern.** Each scoped aggregate has one access predicate, used in three places:

1. **Data, for visibility:**
   - Entities marked `[RecordScoped]` get an EF Core 10 **named query filter** `"RecordAccess"` built from that predicate.
   - It reads `IDataAccessContext` (user id + role codes), which the data portal's scope resolves from `ApplicationContext.User`.
   - It applies to every query: lists, counts, paging, lookups, the fetch before an update or delete, and the history reader. Fetching a hidden record therefore returns **not found**, which doesn't reveal that it exists.
   - Only code marked `[BypassRecordAccess]` may call `IgnoreQueryFilters(["RecordAccess"])`, and an architecture test enforces that.
   - The predicate compiles to an indexed `EXISTS` subquery, for example on `project_member (project_id, user_id)`.
2. **Business, for per-record actions:** object authorization rules on Edit and Delete receive the instance and use the same predicate plus any action-specific condition (for example "only the project lead can close it"). They return a clear "access denied", which is acceptable because the user can already see the record.
3. **Live updates and presence:**
   - Notices carry only type, key and who made the change, never field data.
   - Lists re-query through the filter, so a notice never reveals a hidden row.
   - Editors and presence only exist for users who loaded the record.
   - When a user **loses** access to a record (for example, removed as a project member), the hub sends an access-changed notice for that user. Their open editor closes with a message, their lists refresh, and their draft for that record is discarded.

**Sample:**

- `ProjectEdit` gets a `ProjectMemberList` child list (user + member role).
- Admin and Technical see every project. Client sees only projects they're a member of.
- Admin manages the members.

**Tests:**

- An integration matrix for each role covers visible rows, totals, paging, fetch-by-id of a hidden record (not found), history of a hidden record (denied), and losing access while the editor is open.
- Architecture tests check that every `[RecordScoped]` entity registers the filter and that `IgnoreQueryFilters` appears only in `[BypassRecordAccess]` code.

## 10. Localization

- **Request culture:** `UseRequestLocalization` with en-CA and fr-CA, placed before `MapRazorComponents`. The culture comes from a cookie.
  - The user's preferred language (stored on their profile) sets the cookie at sign-in.
- **Switching language:**
  - It goes through `GET /culture/set?culture=…&redirectUri=…`, which runs `LocalRedirect` and a force-reload, because a circuit's culture is fixed at start.
  - Before navigating, the language toggle **flushes the open form's draft**, and the form restores it after the reload. The toggle therefore never loses data, which the Federal Identity Program rules require.
- **Resources:**
  - `Core/Resources/Labels.resx` + `Labels.fr.resx` hold field names, and `Messages.resx` + `Messages.fr.resx` hold rule, error and UI messages.
  - Strongly-typed keys are generated at build, so keys are referenced with `nameof(Labels.Project_Name)`.
- **The attribute:** `LocalizedDisplayNameAttribute : DisplayNameAttribute` overrides `DisplayName`, reading the key for `CultureInfo.CurrentUICulture`.
  - CSLA reads `PropertyInfo.FriendlyName` again on every get, so labels follow the culture with nothing cached.
  - CSLA's built-in rule messages ship with French (`Resources.fr.resx`).
- **Completeness tests:**
  - Every key exists, non-empty, in both languages.
  - Every registered business property has a `[LocalizedDisplayName]`.
  - Every `*Type` row has both descriptions.
- **GCDS:** components read `lang` once at load, which is one more reason for the full-reload switch. `<html lang>` is set from the culture.

## 11. Live updates, presence and drafts (D5, D6, D16)

### 11.1 Change notifications

- **The change record:** `EntityChange(EntityType, EntityKey, RootType, RootKey, Kind, Version, ChangedByUserId, ChangedByName, OriginId)`. `OriginId` identifies the circuit that made the change, so it ignores its own echo.
- **Publishing (automatic):**
  - The audit interceptor collects the changed aggregate roots.
  - A `DbTransactionInterceptor` flushes them to `IEntityChangeNotifier` **after commit**, or after `SaveChanges` when there's no explicit transaction.
  - Developers never publish by hand.
- **Subscribing:**
  - `IEntityChangeHub.Subscribe(topic, handler)` returns an `IDisposable`.
  - Topics are `(type, key)` for an open editor and `(type)` for a list.
  - The registry is a `ConcurrentDictionary<Topic, ImmutableArray<Subscription>>`. Dispatch only touches that topic's subscribers, which is O(subscribers).
  - Each form or table subscribes once. Fields never subscribe.
- **Backplane:**
  - `InMemoryChangeNotifier` is used for one instance.
  - `PostgresChangeBackplane` (a `BackgroundService`) is used for more:
    - It holds a dedicated connection that `LISTEN`s on `app_entity_changes`.
    - It publishes with `NOTIFY` on a pooled connection after commit.
    - The payload is a small JSON key record, far below the 8000-byte limit.
    - Each instance drops its own echoes by instance ID.
    - On disconnect it reconnects with backoff, then raises `Resync`: every list refreshes and every editor checks its version.
- **Semantics:** at-most-once. Notifications are **hints**. Optimistic concurrency (`xmin`) is the guarantee.
- **Component behaviour:**
  - A list debounces refreshes (500 ms) and keeps sort, page and selection.
  - A clean editor re-fetches, highlights the changed fields and announces "Updated by Jane" to screen readers.
  - A dirty editor shows a banner: "Jane saved changes. Reload / Keep mine (compare)". If the user saves anyway, the save fails the per-aggregate concurrency check and the conflict dialog opens.
- **Conflict dialog (Claude's call):**
  - It fetches a fresh copy and lists every field where "theirs" and "yours" differ, with root fields first and then children matched by key.
  - **Use theirs:** discards my edits.
  - **Apply mine:** reapplies each field I changed onto the fresh copy, including child adds, edits and deletes matched by key. Where both users changed the same field, I pick which value to keep. The result then goes back into the form for review; it is never saved automatically.

### 11.2 Presence

- `IPresenceTracker.Join(topic, user, mode: Viewing|Editing)` returns an `IDisposable`. It's available to subscribers of that topic.
- With several instances, join and leave messages travel over the same backplane. A heartbeat goes out every 30 seconds, and entries expire after 90 seconds.
- The UI shows "Jane is also editing this record".

### 11.3 Drafts

- **Storage:** `IDraftStore` writes to the PostgreSQL table `draft` (user_id, entity_type, entity_key, version, payload, saved_at, expires_at).
  - The payload is the business object serialized with CSLA's serializer and then encrypted with Data Protection.
  - Drafts expire after 8 hours.
- **When drafts are written:**
  - 2 seconds after the last edit, while the form is dirty.
  - Immediately before a language switch or a session-timeout sign-out.
- **When drafts are deleted:** on save, cancel or discard.
- **On opening an editor with a draft:** the user sees "Restore unsaved changes from 10:42?". If the record's version has changed since then, the prompt warns "this record changed since; review before saving".
- **Why not the alternatives:**
  - .NET 10 circuit-state persistence alone only covers evicted circuits, needs JSON (CSLA graphs don't round-trip), and doesn't survive a refresh or restart.
  - Browser storage would put Protected B data on the endpoint.

## 12. Components (code now, UI later)

### 12.1 Hierarchy

Every component inherits `CslaBaseComponent`.

```
CslaBaseComponent                       abstract · ComponentBase · IAsyncDisposable
├── CslaBaseComponent<T>                holds a CSLA object; knows EntityType + EntityKey (IEntityIdentity)
│   ├── CslaForm<T>                     owns AppViewModel<T>, EditContext, subscription, presence, draft,
│   │                                   NavigationLock, shortcuts; cascades CslaFormContext<T>
│   └── CslaTable<TList,TItem>          read-only list; type subscription; server paging/sort/filter
├── CslaFieldBase<TValue>               For="() => Model.Prop"; resolves IPropertyInfo from CslaFormContext
│   ├── CslaTextField · CslaTextArea · CslaNumberField<T> · CslaDateField
│   ├── CslaSelect<TValue> (incl. *Type lists) · CslaCheckbox · CslaCheckboxGroup · CslaRadioGroup<TValue>
├── CslaButton                          Save | Cancel | Delete | New | Custom; permission- and busy-aware
├── CslaTableColumn · CslaTableRow
├── CslaHistory                         (type, key, property?) timeline of changes
└── CslaErrorSummary · CslaToastHost · CslaLiveRegion · CslaPresenceIndicator · CslaChangeBanner
    CslaConflictDialog · CslaConfirmDialog · CslaSessionTimeout · CslaLanguageToggle · CslaAuthorize
```

**`AppViewModel<T> : ViewModel<T>`** fills gaps in CSLA's ViewModel:

- A public `CancelAsync()` that wraps `DoCancel`.
- `IsDirty` and `IsSavable`.
- A `BusyChanged` event (CSLA's `IsBusy` raises no event).
- Per-object, refreshable `CanEdit`/`CanDelete`/`CanCreate`. CSLA's versions are per type and computed once.
- `ManageObjectLifetime = true`, so Cancel is a real undo.

### 12.2 `CslaBaseComponent` contract

| Concern | Provided by the base |
|---|---|
| Identity | `ComponentId` (stable, used for DOM ids and aria links). `CslaBaseComponent<T>` adds `EntityType`, `EntityKey`. |
| Lifetime | `DisposalToken` (cancelled on dispose); subscriptions are disposed automatically; in-flight loads are cancelled when the user navigates away |
| Operations | `RunAsync(name, op)` handles busy state, the tracing span, the correlation id, and error handling: `DataPortalException` is unwrapped, `SecurityException` shows access-denied and logs a security event, `ConcurrencyException` opens the conflict dialog, anything else shows a friendly message with the reference number |
| Notifications | `Toasts` (success, info, warning, error; bilingual), `Announce()` for the screen-reader live region |
| Live updates | `Subscribe(topic, handler)` marshals to `InvokeAsync` and is removed on dispose |
| Security | `ICurrentUser`; helpers `CanRead(prop)` and `CanWrite(prop)` |
| Localization | `L` (IStringLocalizer for Messages); re-renders after a culture change |
| Telemetry | `ActivitySource "CslaStarter.Components"` spans for load, save, delete and refresh, tagged with component and entity type; save/load duration histograms |

### 12.3 Behaviours (all in scope, per Said)

- **Busy state:** form-level and field-level. Shows "Checking…" while an async rule runs. Buttons are disabled while busy.
- **Unsaved changes:**
  - `NavigationLock` handles internal navigation with `CslaConfirmDialog`, and external navigation with `beforeunload`.
  - The language toggle and session timeout go through drafts.
  - Optional per-field markers show which fields changed.
- **Error summary and focus:** on a failed save, `CslaErrorSummary` (rendered with `gcds-error-summary error-links`) lists every error with a link to its field, and focus moves to the summary.
- **All three severities:** errors block saving. Warnings and information messages are shown but don't block. CSLA's `CslaValidator` only pushes errors into the EditContext, so fields read warnings and info straight from `IPropertyInfo`.
- **Hints from rules:** the required marker and `maxlength` come from the rule descriptions, never from markup.
- **Permission-aware buttons:** Save, Delete and New follow `AppViewModel` permissions plus `IsSavable`. Delete asks for confirmation.
- **Undo:** Cancel restores the original values (CSLA n-level undo).
- **Presence indicator, change banner, changed-field highlight** (see §11).
- **Field history:** the history icon on a field opens `CslaHistory` filtered to that property.
- **Session-timeout warning** (see §9).
- **Draft recovery** (see §11.3).
- **Keyboard shortcuts:** Ctrl+S saves and Esc cancels, scoped to the active form. A JS module ignores Esc inside open dialogs.
- **Masked sensitive fields:**
  - `[Sensitive(RevealRoles = new[] { UserRoleType.Admin })]` renders a masked value on the server, so the real value never reaches the browser until revealed.
  - Reveal requires a role and logs a security event.
- **Per-component tracing** (see §12.2).
- **Screen-reader announcements:** for saved, updated by another user, restored, and session warning.

### 12.4 GC Design System interop

- **Assets:** GCDS 1.7.0 assets are self-hosted under `Components/wwwroot/gcds/`. A JS initializer `CslaStarter.Components.lib.module.js` (`afterWebStarted`) registers custom events through `blazor.registerCustomEventType`:
  - `gcdsinput` (from `gcdsInput`)
  - `gcdschange` (from `gcdsChange`)
  - `gcdsblur` and `gcdsfocus`
  - `gcdsclick` (from `gcdsClick`)
  - `gcdstablestate` (from `gcdsTableStateChange`)

  Each custom event has a different name from the browser event, which avoids double-firing in .NET 10 and the name-collision error in .NET 11. The C# side is `[EventHandler]` attributes in a static `EventHandlers` class, with `ChangeEventArgs`-like arguments carrying `e.detail`.
- **Field attributes:** every field sets `validate-on="other"`, and forms set `novalidate`, so CSLA is the only validator.
  - `error-message` comes from `IPropertyInfo.ErrorText`, and `label` comes from `FriendlyName`.
  - `hint` comes from a parameter, or from the warning or information text.
  - `required` and `maxlength` come from the rules.
  - `readonly` is used where GCDS supports it, which is gcds-input only. Elsewhere a no-write field renders as text.
- **Known traps:**
  - Never bind a changing `class` on a GCDS host, because Stencil's `hydrated` class would be lost. State goes on a wrapper element or a static attribute.
  - Object props (`options`, `error-links`, `columns`) are serialized to JSON strings.
  - GCDS forces `disabled=false` when `required` is set, so read-only/no-write fields drop `required`.
  - Every field host gets an `id`, so the error-summary links can focus it.
- **Tables:** `CslaTable` renders a semantic `<table>` styled with GCDS tokens, plus `gcds-pagination` for server-side paging. `gcds-table` is client-side only (TanStack) and its slots clash with Blazor's DOM diffing.
- **Shell:** the layout shell (gcds-header with signature, `signature-has-link="false"`, lang toggle and skip-to; gcds-footer compact with privacy and terms links; wordmark) is part of the UI phase. Its required elements are listed here so the code exposes what the shell needs: the language href and the account slot data.
- **The reconnection modal** from the .NET 10 template is kept and restyled later.

### 12.5 `.razor` policy for this phase

- Every component has `X.razor` and `X.razor.cs`; the code goes in `.razor.cs`. A test checks the pairs.
- **(Claude's call)** `.razor` holds only the bare bound element: for example one `<gcds-input>` with its attributes and events, or a `<table>` skeleton. There is no layout, styling or page composition.
- Sample pages are functional but unstyled.
- The UI phase replaces the markup without touching the code-behind.

## 13. Sample features (D12)

| Feature | Demonstrates |
|---|---|
| **Users** (Admin) | `UserList` (read-only, paged, filter by name or role) and `UserEdit` (editable root) with a `UserRoleAssignmentList` child list. `UserRoleType` drives a dropdown. `EmployeeNumber` is a masked sensitive field. Role changes revalidate open sessions. |
| **My profile** | Editing your own record (object-level rule: users can edit only themselves) and setting your preferred language |
| **Projects** | `ProjectList` (read-only, paged, sorted, filtered by `ProjectStatusType`, record-scoped). `ProjectEdit` (root) with a `ProjectTaskList` (`BusinessListBase` children) and a `ProjectMemberList` that drives record-level access (§9.1). `CloseProjectCommand` (`CommandBase`). History per record and per field. An async rule (unique project code). |
| **Property-level permissions** | `Budget`: Admin and Technical can read it, only Admin can write it. Client gets hidden Budget and read-only projects. Technical can edit tasks but not Budget. |

**(Claude's call)** Users, roles and `*Type` support are core. Projects/Tasks is always included as the reference implementation.

- It lives in `Sample` folders and namespaces in each project, with its own migration (`AddProjectsSample`) after the core migration.
- A `remove-sample` recipe and Claude skill deletes it cleanly, including the migration and snapshot entries.

## 14. Hosting and operations

| | On-prem IIS | Azure App Service |
|---|---|---|
| Runtime | .NET 10 Hosting Bundle (ANCM, in-process) | Linux or Windows, .NET 10 |
| WebSockets | Enable the IIS WebSocket Protocol feature | Turn on (it's off by default) |
| Several instances | ARR session affinity | ARR affinity on (default) |
| Recycling | App pool idle timeout off; overlapping recycle; circuits end on recycle (drafts cover this) | Always On |
| Secrets | Protected configuration / secret store | Key Vault references and managed identity |
| Data Protection keys | PostgreSQL, protected with a certificate | PostgreSQL, protected with Key Vault |
| Database | On-prem PostgreSQL with TLS and disk encryption | PostgreSQL Flexible Server (Canada Central/East) with Entra auth |
| Change backplane | PostgreSQL LISTEN/NOTIFY | PostgreSQL LISTEN/NOTIFY (no PgBouncer in transaction mode for the listener) |

- **Health endpoints:** `/health/live` (process) and `/health/ready` (PostgreSQL via `AspNetCore.HealthChecks.NpgSql`, backplane listener connected, type seed consistency).
- **Azure SignalR Service** isn't used. It doesn't help on-prem, it adds a PaaS hop that would need a Protected B assessment, and circuits stay on our servers anyway. It's documented as an option.

## 15. Observability

- **OpenTelemetry 1.21.0:**
  - Tracing covers ASP.NET Core, HttpClient, Npgsql, the Blazor sources `Microsoft.AspNetCore.Components` and `…Server.Circuits`, and `CslaStarter.*`.
  - Metrics cover ASP.NET Core, runtime, Npgsql and the Blazor meters.
  - Logs go through `ILogger`.
  - Everything is exported with `UseOtlpExporter()`.
- **Correlation:** the trace ID is the user-facing reference number, and it is written into `audit_event.correlation_id`.
- **Serilog:** an opt-in rolling JSON file sink for IIS sites with no OTLP collector (Serilog.AspNetCore + Sinks.OpenTelemetry, so OTel still gets the logs).

## 16. Testing strategy

| Layer | Tooling | Covers |
|---|---|---|
| Business | xUnit v3 4.0.3, Csla.Testing (`CslaTestHost`, `BusinessRuleTester`, `AuthorizationRuleTester`, `RuleCache.Clear`), in-memory fake DALs | every rule, every role × property × action, the data portal flow |
| Components | bUnit 2.15.2 | labels, errors, warnings and info, hidden or read-only by permission, busy, dirty guard, error summary, button permissions, change banner, a11y attributes |
| Integration | WebApplicationFactory (`Microsoft.AspNetCore.Mvc.Testing` 10.0.15), Testcontainers.PostgreSql 4.16.0, `TestAuthHandler` with configurable user and roles | migrations, the DAL against real PostgreSQL, xmin conflicts, the audit interceptor and append-only grants, drafts, **two app instances exchanging LISTEN/NOTIFY**, claims transformation, policies, security headers |
| End-to-end | Playwright 1.64.1, mock-oauth2-server 6.1.5 as the fake Entra ID, `UseKestrel()` + `StartServer()` | real OIDC sign-in and sign-out, roles, language switch keeping edits, two browsers seeing live updates and presence, session timeout, axe-core WCAG 2.1 AA scan of the sample pages |
| Architecture | NetArchTest-style rules + Roslyn syntax checks | the reference rules (§5), the CSLA class layout (§6.2), `.razor`/`.razor.cs` pairs, resource completeness, `*Type` seed consistency, no `MarkupString` on user data, no Claude paths on `main` builds |

CI runs every layer except E2E on each pull request. E2E runs on merge to `claude` and nightly. The build fails on any warning, vulnerable package or coverage drop below the agreed gate (80% line coverage for Business and Components).

## 17. Repository, branches, CI/CD

- **Remote:** `github.com/said-rustom-cirnac/csla-starter` (public, user-owned, empty; secret scanning and push protection on) **(Said)**.
  - Because the repository is public, the `claude` branch is publicly readable too. `main` stays free of Claude files, but they aren't hidden.
- **Branches:** `main` is protected and never committed to directly. `claude` is the integration branch. Feature branches are `DEV-<n>-<slug>`, cut from `claude`, and enter it through a PR.
- **Who may push to `main` (Claude's call):** a repository ruleset on `main` blocks direct pushes and force-pushes. Its only bypass actor is a deploy key (stored as an Actions secret), which the sync workflow uses.
- **Sync to main** (`.github/workflows/sync-main.yml`):
  - Runs on every push to `claude` after CI is green.
  - Builds `main`'s next commit as a **single-parent snapshot**: the `claude` tree minus the Claude-only paths (`.claude/`, `**/CLAUDE.md`, `docs/claude/`, `docs/superpowers/`, `.impeccable/`).
  - The commit message references the `claude` SHA range.
  - Snapshot commits never conflict, and Claude files never enter `main`'s history.
- **Guard** (`guard-main.yml`): fails any `main` build that contains a Claude-only path.
- **CI** (`ci.yml`):
  - restore (locked) → format check → build (warnings as errors) → unit, bUnit, integration and architecture tests → coverage → NuGet audit → CodeQL.
  - On release: SBOM, EF migration bundle, and publish artifacts for IIS (zip) and App Service.
- **Template (Said):** `.template.config/template.json`, packaged from the **`claude`** branch. `sourceName: CslaStarter`, with options for app name and `--claude` (default `true`).
  - With `--claude false`, the template excludes `.claude/`, `**/CLAUDE.md`, `docs/claude/`, `docs/superpowers/`, `.impeccable/`, `sync-main.yml` and `guard-main.yml`.
  - The Claude guides are therefore written for *any app made from the template*, not about this repository. Anything specific to the template's own development lives in `docs/claude/template-dev.md`, which the template always excludes.
  - The release workflow builds the template package (`.nupkg`) and attaches it to the GitHub release, together with the SBOM.
- **Licence:** not chosen yet. This is a decision for the department before the first public release, and the template ships without a licence file until then.
- **Jira:** a new Epic named "CSLA-starter". The Story for this design is created when the design is approved. Commits start with `DEV-<n>`.

## 18. Claude documentation (claude branch only)

- **Root `CLAUDE.md`, short:**
  - The Jira epic line.
  - Build, test and run commands.
  - The architecture map with allowed references.
  - The golden rules: class layout, no UI-side validation, `obj = await obj.SaveAsync()`, `Func<string>` messages, `[LocalizedDisplayName]`, no Claude files on main.
  - Pointers to the detailed guides.
  - The rule **"update the relevant CLAUDE.md and guide in the same change"**.
- **`docs/claude/`:**
  - `business-objects.md`, `data-access.md`, `components.md`, `localization.md`, `security.md`, `realtime.md`, `testing.md`, `gcds.md`
  - `recipes/` (add a feature end to end, add a `*Type`, add a component, add a migration)
- **Per-project `CLAUDE.md`:** purpose, allowed references, conventions, gotchas (the §6.4 CSLA gotchas live in Business's).
- **`.claude/`:**
  - Settings with allowed `dotnet` and `docker` commands.
  - Three project skills: `new-business-object`, `new-type`, `new-component`. Each scaffolds files following the conventions plus their tests.
- **Freshness check:** `Architecture.Tests` checks that each project folder has a `CLAUDE.md` whenever `.claude/` exists. It's skipped on `main` and in apps made with `--claude false`.

## 19. Pinned versions

| Package | Version |
|---|---|
| Csla, Csla.AspNetCore, Csla.Blazor, Csla.Testing | 10.2.0 |
| Microsoft.AspNetCore.* / Microsoft.Extensions.* | 10.0.15 |
| Microsoft.Identity.Web | 4.18.0 |
| Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL, Npgsql.DependencyInjection, Npgsql.OpenTelemetry | 10.0.3 |
| EFCore.NamingConventions | 10.0.1 |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.15 |
| Azure.Extensions.AspNetCore.DataProtection.Keys | 1.6.5 |
| OpenTelemetry.* | 1.21.0 |
| AspNetCore.HealthChecks.NpgSql | 9.0.0 |
| xunit.v3 | 4.0.3 |
| bunit | 2.15.2 |
| Testcontainers.PostgreSql, Testcontainers.XunitV3 | 4.16.0 |
| Microsoft.Playwright, Microsoft.Playwright.Xunit.v3 | 1.64.1 |
| mock-oauth2-server (container) | 6.1.5 |
| @gcds-core/components · @gcds-core/fonts | 1.7.0 · 1.1.1 |

## 20. Delivery phases

1. **Foundation:** repo, branches and sync, solution skeleton, build settings, CI, Claude docs skeleton.
2. **Data and security core:** PostgreSQL, EF Core, migrations, DB roles, audit, security events, Data Protection, Entra + mock OIDC, claims transformation, headers/CSP, culture, session timeout.
3. **Business layer:** base classes, `CslaType`, types, rules and authorization, sample domain.
4. **Realtime:** change hub, PostgreSQL backplane, presence, drafts.
5. **Components (code):** base family, GCDS interop, every behaviour in §12.3, sample pages (unstyled).
6. **Hardening and packaging:** E2E + axe, architecture tests, `dotnet new` template, guides complete.
7. **Later:** UI phase (GC Design System layout and styling with Impeccable).

## 21. Left to the builder

- Exact names of internal classes and files, as long as they follow the conventions above.
- Debounce and heartbeat tuning within the stated ranges.
- How strongly-typed resource keys are generated (an MSBuild `StronglyTypedLanguage` or a source generator).
- The JSON shape of the NOTIFY payload.
- Which architecture-test library to use.
- The `axe` package choice.

## 22. Risks

| Risk | Mitigation |
|---|---|
| Blazor Server is stateful: sticky sessions, memory per user, drops on recycle | Drafts, circuit limits, a load test before the first production app, documented sizing |
| LISTEN/NOTIFY is at-most-once | Notifications are hints; resync on reconnect; `xmin` concurrency guarantees correctness |
| App-level audit is bypassed by direct database edits | Insert-only grants, an UPDATE/DELETE-blocking trigger, pgAudit guidance for DBA sessions |
| No official GCDS Blazor package | Thin wrappers pinned to 1.7.0 (1.x API stability promise) and tested with bUnit and Playwright |
| CSLA is a niche skill | Guides, recipes, scaffolding skills, Csla.Testing; MIT-licensed and active (10.2 shipped Oct 2026) |
| .NET 10 support ends Nov 2028 | Upgrade path to the next LTS is documented; Central Package Management keeps upgrades small |
| Entra tenant needed to develop | mock-oauth2-server in docker-compose and tests |
