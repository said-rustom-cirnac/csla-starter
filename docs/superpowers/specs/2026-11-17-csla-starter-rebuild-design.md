# CSLA Starter Rebuild — Technical Design

- **Date:** 2026-11-17
- **Status:** Approved by Said on 2026-11-17 (Jira DEV-39)
- **Owner:** Said Rustom
- **Replaces:** `2026-11-12-csla-starter-design.md` (the first build). This document stands on its own.

Decisions marked **(Said)** came from Said. Decisions marked **(Claude's call)** were made during design and can be overturned in review.

---

## 1. Intent

Rebuild the starter from scratch on a new branch, using CSLA the way the framework is designed to be used.

The result is a `dotnet new` template for Government of Canada line-of-business web apps: .NET 10, Blazor Server, CSLA 10.2, EF Core on PostgreSQL, Entra ID, English and French, Protected B.

**Who it is for (Said):** one human developer who will build several applications on it. Every design choice is judged against four words: **reliable, secure, understandable, manageable**. When two options are equal on the first two, the one with less code to own wins.

**Why rebuild.** The first build's business layer was sound, but:

- nothing ran in a real browser until the end, and that is where the defects were;
- the form component carried drafts, presence and field-by-field merging, and unwound CSLA's undo levels by hand;
- it ignored framework features that remove code: the property source generator, validation attributes, and the data portal's extension points;
- adding one field touched five projects.

## 2. Decisions for this rebuild

| # | Topic | Decision | By |
|---|---|---|---|
| R1 | CSLA version | 10.2.0, written so the move to 11 is a package update (§6.8) | Said |
| R2 | Distribution | A copy template (`dotnet new`), not shared packages | Said |
| R3 | Class layout | Properties, then business rules, then data portal operations. Registrations are generated. | Said |
| R4 | Collaboration | Keep live updates and history. Drop drafts and presence for now. | Said |
| R5 | Conflicts | No field-by-field merge. "This record changed: reload." | Claude's call |
| R6 | Tiers | Data and web on one server. No remote data portal for now; nothing may block adding it later. | Said |
| R7 | Security and UI scope | Row-level security, server-side sessions, rate limits, error boundary, transactional change notices, tamper-evident audit, permissions matrix, and the real user interface are all in | Said |
| R8 | Projects | Three projects, organised by feature (§5) | Claude's call |
| R9 | First delivery | One screen end to end in a real browser before anything else (§17) | Said |
| R10 | Children | Child objects save themselves inside the root's unit of work (§6.5) | Said |
| R11 | Rules | Shape rules as attributes on the property; business logic as rule classes (§6.3) | Claude's call |
| R12 | Starter updates | `Platform` folders are starter-owned; a script diffs them against a newer starter (§13.3) | Claude's call |
| R13 | Translations | A database lookup, `TranslationType` (Code, SourceString, EnDesc, FrDesc, IsActive, AddedDate). Code refers to a translation by its source string. No `.resx` files. (§9) | Said |
| R14 | Missing translations | Added at run time: English is the source string, French is `fr-` plus the source string, so untranslated rows are easy to spot and filter | Said |
| R15 | Error messages | A lookup, `ErrorMessageType`, with code constants. Source code refers to messages only through those constants. (§9.3) | Said |
| R16 | Admin pages | Translations, Error Messages and Users ship in the template. Admins edit text and IsActive only; rows are created by code. Users shows roles, last sign-in and who is online. (§12) | Said |
| R17 | Shipping translations | Seed files in the repository are what travels between environments; a command exports new rows from a development database into them (§9.4) | Claude's call |
| R18 | Record history | One common component shows a record's changes: date, changed by, field, old value, new value. It covers the record's children. Every form gets it without page code. (§11.5) | Said |
| R19 | Protected B | The starter ships the application-level controls with a control mapping, and states what the host must provide (§8.7) | Claude's call |
| R20 | System-use notice | A tab of the Administration page switches a sign-in notice on or off and edits its text in both languages (§8.8) | Said |
| R23 | Who can sign in | Anyone in the Entra tenant. A first sign-in creates the account with no roles; what a user sees is decided only by roles and record-level access (§8.1) | Said |
| R24 | Account expiration | An optional expiration date on a user, set by an Admin; after it, sign-in is refused (§8.1) | Said |
| R25 | Claude guidance | A `CLAUDE.md` at the root and in every project, guides the root points to, a question checklist and full outcome tests for custom rules, all validated by tests after every task (§16.1) | Said |
| R22 | Administration page | One page to manage everything about users, translations, error messages and the system-use notice, one tab each, with room for an app's own options. Admins only by default. (§12) | Said |
| R21 | Declined controls | No view logging, no column encryption, no automatic deactivation of unused accounts (§18) | Said |

Carried over unchanged from the first build **(Said)**: Blazor Server with prerendering off; PostgreSQL behind DAL interfaces; Entra ID with a mock for development and tests; several roles per user, stored in our database; every change audited with a History component; record-level access; GC Design System 1.7.0, self-hosted; on-premises IIS or Azure App Service; `*Type` lookups with code constants; `.razor` plus `.razor.cs`; the `claude` → `main` sync; the `--claude` template option.

Changed from the first build **(Said)**: translations and error messages move from `.resx` files to database lookups (R13 to R15).

## 3. Goals, non-goals, anti-goals

**Goals**

1. A generated app that builds, passes its tests, signs in, and shows a finished bilingual GC Design System interface on first run.
2. A business class that reads top to bottom as: what it holds, what its rules are, how it loads and saves.
3. Adding a field changes only that feature's own files (business class, DTO, entity, DAL, page) and adds a migration. Its label needs no extra step: the translation row is added on first use. Adding a feature starts from a scaffold command that creates new files and edits no existing source file.
4. Security controls that each live in one place and fail closed.
5. Every claim in this document is proven by an automated test, and every page by a real-browser test, from the first slice on.
6. A handbook a human can follow without Claude.

**Non-goals (this version)**

- Drafts, presence, field-by-field merge, keyboard shortcuts (§18).
- A remote data portal or separate app server; shared NuGet packages.
- WebAssembly or Auto render modes, multi-tenancy, a public REST API, mobile apps.
- Admin screens for other `*Type` values (their codes and rows are owned by code). Translations and error messages are the exception: their text is editable (R16). File storage.
- CSLA 11 itself.

**Anti-goals (it fails if…)**

- A rule or permission is written anywhere but a business object.
- A page works in tests but not in a browser.
- A shipped page shows English, or an `fr-` placeholder, in French mode.
- A security control can be skipped by a page or query that forgets it.
- A hidden record's existence can be inferred (counts, paging, notices, history).
- Claude files appear on `main`.
- The template carries demo shortcuts an app could ship by accident.
- One developer cannot explain a `Platform` file after reading it and its handbook page.

## 4. Constraints

| Item | Value |
|---|---|
| Runtime | .NET 10 (LTS). Microsoft.* packages pinned to 10.0.x. No 11.x packages. |
| CSLA | `Csla`, `Csla.AspNetCore`, `Csla.Blazor`, `Csla.Testing`, `Csla.Generator.AutoImplementProperties.CSharp`, all 10.2.0 |
| UI host | Blazor Web App, global Interactive Server, `prerender: false` |
| Database | PostgreSQL 18; EF Core 10 with Npgsql |
| Identity | Microsoft Entra ID; mock-oauth2-server 6.1.5 in development and tests |
| UI kit | `@gcds-core/components` 1.7.0 and `@gcds-core/fonts` 1.1.1, vendored (already in the repo, written by `scripts/update-gcds.sh`) |
| Hosting | On-premises IIS or Azure App Service, one instance or several |
| Security | Protected B baseline, aligned to ITSG-33 |
| Accessibility | WCAG 2.1 AA |
| Languages | en-CA, fr-CA |

Other pinned versions stay as in `Directory.Packages.props` today.

## 5. Solution structure

Three projects. Each has two top-level folders:

- **`Platform`** is starter-owned and the same in every app: the framework code, the component kit, and the core features every app needs (users and roles, sessions, translations, error messages, audit, history).
- **`Features`** is the app's own code, one subfolder per feature. The starter ships one: the Projects sample.

```
src/
  CslaStarter.Domain     CSLA business objects, rules, DAL contracts and DTOs
    Platform/            base classes, rule attributes, HasRole, CslaType, data portal hooks, the translator,
                         contracts (ICurrentUser, IUnitOfWork, IEntityChangeHub, exceptions, paging)
    Platform/Users/      UserEdit, UserList, UserProfile, UserRoleType (Admin), IUserDal + DTOs
    Platform/Texts/      TranslationType, ErrorMessageType and their editable objects (§9)
    Platform/Notice/     the system-use notice (§8.8)
    Platform/History/    the audit history objects
    Features/Roles/      the app's own roles (the other half of UserRoleType, §6.4)
    Features/Projects/   the sample (§12)
  CslaStarter.Data       EF Core and PostgreSQL
    Platform/            AppDbContext, unit of work, audit, record access, change notices, sessions,
                         Data Protection keys, database startup checks, and the DALs of the core features
    Features/<Feature>/  entities, configuration, DAL implementation
    Seed/                translations.json, error-messages.json (§9.4)
    Migrations/
  CslaStarter.Web        host, components, pages
    Platform/            sign-in, headers, sessions, rate limits, localization, realtime listener,
                         health, telemetry, startup checks, commands (bootstrap-admin, doctor, audit tools)
    Platform/Components/ the Csla* component kit and the layout shell
    Platform/Pages/      My profile, Administration (tabs: Users, Translations, Error messages, System notice),
                         home and the error pages
    Features/<Feature>/  the app's pages
    wwwroot/gcds/        the vendored design system
tests/
  CslaStarter.Domain.Tests        rules, permissions, data portal flow, with fake DALs
  CslaStarter.Web.Tests           bUnit component and page tests
  CslaStarter.Integration.Tests   real PostgreSQL (Testcontainers) and the real host
  CslaStarter.E2E.Tests           Playwright, mock Entra ID, axe
  CslaStarter.Architecture.Tests  boundaries, conventions, documentation
```

**Boundaries**, enforced by `Architecture.Tests`:

- `Domain` references no EF Core, Npgsql or ASP.NET Core assembly.
- The compiled `Data` assembly does not reference the `Csla` assembly. It sees only DAL contracts and DTOs.
- Components and pages never use `AppDbContext` or any `Data` type. They reach data only through the data portal.
- A feature may use anything in `Platform` (so a project's member list can point at users). A feature never references another feature's DAL or entities; features share through business objects. `Platform` never references `Features`.
- Business data is written only through the unit of work (§7.2). Only `Platform` code calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL.

**By convention, so a new feature edits no existing source file:**

- DAL implementations are registered by scanning the `Data` assembly for `I*Dal` implementations.
- Entity configurations load with `ApplyConfigurationsFromAssembly`; entities are reached with `Set<T>()`.
- Navigation and breadcrumbs are built from an attribute on each page (title, order, required permission), found by reflection.

**Build settings** stay as today: nullable, warnings as errors (including the CSLA analyzers), NuGet audit, central package management with lock files, deterministic builds.

## 6. Business layer

### 6.1 Configuration

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .AddServerSideBlazor(b => b.UseInMemoryApplicationContextManager = true)   // stated, not assumed
    .DataPortal(dp =>
    {
        dp.UseLegacyOperationMethods = false;
        dp.AddServerSideDataPortal(server => server
            .RegisterAuthorizerProvider<AppAuthorizer>()
            .RegisterExceptionInspector<AppExceptionInspector>()
            .AddInterceptorProvider<AppDataPortalInterceptor>()
            .RegisterDashboard<OpenTelemetryDashboard>());
    }));
```

Verified in the 10.2 source and relied on:

- Every root data portal call from a circuit runs in its **own DI scope** (`LocalProxyOptions.UseLocalScope` is true). One scope is one `DbContext` and one unit of work.
- `RevalidatingInterceptor` is registered by default: rules run again on the server at save.
- `PropertyInfo.FriendlyName` reads `DisplayAttribute` first, then `DisplayNameAttribute`, on every access.
- The `DataAnnotation` rule calls the attribute at run time with the property's friendly name, so a message can be looked up in the user's language each time it is shown.

### 6.2 Class shape

```csharp
[CslaImplementProperties]
[HistoryOf("Project")]
public sealed partial class ProjectEdit : AppRootBusinessBase<ProjectEdit>
{
    // 1. Properties: what the object holds, with its label and shape rules
    public partial Guid Id { get; private set; }

    [Label("Project code"), RequiredField, MaxLengthOf(20)]
    [Matches("^[A-Z0-9-]+$", ErrorMessageType.ProjectCodeFormat)]
    public partial string Code { get; set; }

    [Label("Budget")]
    public partial decimal? Budget { get; set; }

    public partial ProjectTaskList Tasks { get; private set; }

    // 2. Business rules: logic and permissions
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new UniqueProjectCode(CodeProperty, IdProperty));
        BusinessRules.AddRule(new HasRole(AuthorizationActions.WriteProperty, BudgetProperty, UserRoleType.Admin));
    }

    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules() { /* create, get, edit, delete */ }

    // 3. Data portal operations: how it loads and saves
    [Create] private void Create(...) { }
    [Fetch]  private async Task Fetch(Guid id, [Inject] IProjectDal dal, ...) { }
    [Insert] private async Task Insert([Inject] IProjectDal dal, [Inject] IUnitOfWork work) { }
    [Update] private async Task Update([Inject] IProjectDal dal, [Inject] IUnitOfWork work) { }
    [DeleteSelf] private async Task DeleteSelf([Inject] IProjectDal dal, [Inject] IUnitOfWork work) { }
}
```

- The generator writes the `XProperty` registrations and the accessors. Business classes are `sealed partial`.
- A Roslyn test enforces the order: properties, rules, operations. `*Type` classes put their `public const int` codes first.
- Attribute names in this example are illustrative; the builder picks final names.

### 6.3 Rules

- **Shape rules are attributes on the property:** required, maximum length, range, pattern. They are our subclasses of the standard `System.ComponentModel.DataAnnotations` attributes. Each takes its message from `ErrorMessageType` by constant (§9.3). CSLA turns them into rules.
- **Business logic is a rule class** added in `AddBusinessRules`: cross-field checks, lookups, calculations, anything with a condition. A rule names its message by `ErrorMessageType` constant, resolved when the rule runs, so it follows the language.
- **Labels:** `[Label("Project code")]` names the translation's source string (§9.2). It must resolve through CSLA's `FriendlyName`.
- **Severities:** error, warning and information are all used and shown.
- **Async rules** (for example "code is unique") call a CSLA command through the data portal, never a DAL directly, so they keep working if the portal becomes remote (R6). The command's DAL uses its own short-lived context, so the rule also runs safely when the server revalidates inside a save. The unique index stays as the final guard.
- **UI hints** (required marker, `maxlength`) are read from the attributes, never repeated in markup.

### 6.4 Permissions

- **Roles:** `UserRoleType` is a partial class. `Platform` declares `Admin = 1`, which the starter itself needs (`bootstrap-admin`, the admin pages). The app declares its own roles in `Features/Roles`; the starter ships `Client = 2` and `Technical = 3` for the sample.
- `HasRole(action, [property], params int[] roleCodes)` is the one role rule. CSLA evaluates one rule per (property, action), so a single rule lists every allowed role.
- Object-level rules cover create, get, edit and delete. Per-record conditions ("only a member may edit") are object rules that receive the instance.
- **Fail closed:** `AppAuthorizer` runs CSLA's standard checks and also refuses any business type that declares no object-level rules. A convention test fails the build for the same reason.
- No read permission means the field is hidden; no write permission means read-only text.
- `ICurrentUser` (user id, object id, display name, role codes, `HasRole`) reads CSLA's `ApplicationContext.User`.

### 6.5 Loading and saving

- **Data access model:** data portal operations call an injected DAL interface with DTOs. This is CSLA's preferred model.
- **Children save themselves.** The root's `[Insert]`/`[Update]` stages its own row, then calls `FieldManager.UpdateChildren`. Each child has `[InsertChild]`, `[UpdateChild]` and `[DeleteSelfChild]`. CSLA's own new, dirty and deleted tracking decides what runs; no DAL compares key lists.
- **One unit of work per save.** DAL write methods only stage changes. Nothing reaches the database until the root calls `IUnitOfWork.CommitAsync()`, which writes everything, the audit rows and the change notices in one transaction (§7.2).
- **The object is updated in place** after the commit (new version, any database-assigned values). There is no second fetch.
- **Keys** are `Guid` version 7, created in `[Create]`, so children never wait for a database id.
- **Concurrency is per aggregate:** every save checks the root row's version (PostgreSQL `xmin`) against the one the user loaded, and touches the root even when only children changed.
- **Saving returns a new object:** `obj = await obj.SaveAsync()` everywhere. The CSLA analyzer enforces it.

### 6.6 Base classes (`Domain/Platform`)

Kept small: `AppRootBusinessBase<T>` (identity, `Version`), `AppChildBusinessBase<T>`, `AppListBase<TList,TItem>`, `AppReadOnlyBase<T>`, `AppReadOnlyListBase<TList,TItem>` (paging), `AppCommandBase<T>`, and `CslaType<T>` / `CslaTypeList<TList,T>`.

- **The names are not free.** CSLA 10.2's property generator recognises a class by its direct base class's *name*: it must contain `BusinessBase`, `ReadOnlyBase` or `CommandBase` (verified in the generator's source, `SerializationPartialBuilder.cs`, and by a failed build with `AppRootBase<T>`). Any base class for a type with generated properties must keep one of those words in its name.

- **`*Type` lookups:** every type has `Code`, `EnDesc`, `FrDesc`, `IsActive` and `AddedDate`, and a `Description` that follows the language. Types shown in dropdowns also have `SortOrder`. Each type has its own table. The DAL caches the rows; CSLA objects are never shared between users.
  - **Code-owned types** (for example `UserRoleType`, `ProjectStatusType`, `ErrorMessageType`): codes are `public const int`, and a test fails unless constants and rows match exactly.
  - **`TranslationType`** is keyed by `SourceString`; its `Code` is assigned by the database (§9).
- Audit stamps (created and modified, by and on) are database columns set by the audit interceptor. They are not business properties; History shows them.

### 6.7 Data portal hooks (`Domain/Platform`, registered in §6.1)

| Hook | Job |
|---|---|
| `AppAuthorizer` | Standard checks, plus refusal of types with no object-level rules. Records a security event for what it refuses. |
| `AppExceptionInspector` | Replaces DAL exceptions with typed exceptions the interface understands: duplicate value (naming the property), concurrency, not found. No per-object `try`/`catch`. |
| `AppDataPortalInterceptor` | One structured log line and one trace span per operation (type, operation, duration, outcome). Logging only. |
| `OpenTelemetryDashboard` | CSLA's own metrics: calls, failures, duration |

**Refused access** is recorded where it actually surfaces, because CSLA checks most permissions on the calling side before the server-side portal runs:

- `CslaBaseComponent.RunAsync` records a security event for every `SecurityException` it catches (object-level and property-level).
- `AppAuthorizer` records what it refuses on the server side.
- A fetch that ends in "not found" is logged as a structured warning with the user and the key, so a burst of probing can be alerted on. It is not stored as a security event **(Claude's call)**.

**A duplicate value** does not make the object invalid: the form shows the typed exception's message against the named field and in the error summary.

### 6.8 Ready for CSLA 11

- Every class with operations is `partial`; every operation method has its attribute; no legacy method names.
- Code never depends on how many times, or in what order, rules run.
- The one view-model subclass is the only place that overrides CSLA view-model members.
- No lazy-load `Task<P>` overloads.
- The upgrade is then: remove the generator package, bump the version, read the upgrade guide.

## 7. Data layer

### 7.1 Basics (carried over)

- One `AppDbContext`: Npgsql, snake_case names, `xmin` as the concurrency token.
- Two database roles: `app_owner` runs migrations and the owner-only commands; `app_runtime` runs the app with DML only, insert-and-select only on audit and security tables, no DDL.
- Migrations ship as an EF bundle and run from the deployment pipeline. The app never migrates itself outside Development.
- TLS to the database. On Azure, Entra authentication with no password. Secrets never in `appsettings`.
- Data Protection keys in PostgreSQL, protected by a certificate (IIS) or Key Vault (Azure).

### 7.2 Unit of work

**One data portal operation is one DI scope, one `DbContext` and one database transaction**, for reads as well as writes. DALs get the context from the unit of work, never directly.

- The transaction starts on first use and sets the current user's id and role codes as **transaction-local** settings, which row-level security reads (§7.4). Transaction-local settings cannot leak to another user through any connection pool or pooler mode.
- A read operation ends its transaction when the scope ends.
- `IUnitOfWork.CommitAsync()` is the only code that writes business data. In the same transaction it:
  1. checks the root version and touches the root;
  2. writes the staged changes;
  3. writes the audit rows (§7.5);
  4. issues `pg_notify` for each changed aggregate root (§10).

PostgreSQL delivers a `NOTIFY` only if its transaction commits. So a change and its notice succeed or fail together, with no outbox table and no dispatcher **(Claude's call: this meets the outbox goal with less machinery)**.

**Platform stores write on their own**, each through a short-lived context outside any business transaction: Data Protection keys, sessions, security events, audit seals, and run-time translation rows. A refused-access event must commit even when the business operation does not.

### 7.3 Record-level access, first layer: the query filter (carried over)

- Entities marked `[RecordScoped]` get an EF Core named query filter built from one access predicate per aggregate. It reads `IDataAccessContext` (user id and role codes).
- It applies to every query: lists, counts, paging, the fetch before an update, history. A hidden record is "not found".
- Per-record edit and delete permissions are CSLA object rules.

### 7.4 Record-level access, second layer: row-level security (new)

**What it protects against:** a query that forgets the filter. It is not a defence against a compromised app process, which could set any user's settings.

- **Tables covered:** the root table and every child table of a `[RecordScoped]` aggregate, and the audit tables (they hold a hidden record's old and new values).
- **Commands covered:** select, insert, update and delete all use the visibility predicate. Who may *change* a visible record stays in CSLA rules.
- **Policies call helpers.** A policy never queries a scoped table directly (that recurses). Each aggregate has one `SECURITY DEFINER` function, for example `app.can_see_project(id)`; child tables call it with their root id; audit rows go through one dispatcher, `app.can_see_record(root_type, root_key)`, which is true for unscoped types.
- **Settings:** the policies read the transaction-local user id and role codes (§7.2). With none set, they match nothing (fail closed).
- **System work** (the seal job, `verify-audit`, sign-in provisioning) runs in a system context that sets an explicit "sees all" setting. Only `Platform` code can open one.
- `app_runtime` cannot bypass policies. `app_owner` can, for migrations and owner commands.
- **Cross-record checks** (a unique code among all projects) are narrow `SECURITY DEFINER` functions, not a general bypass.
- **New records:** an insert returns the new row's version, and PostgreSQL applies the select policy to that returned row. Where visibility comes from child rows (a member list), the creating transaction must still be able to see the root it just inserted. The policy pattern allows this, and a test covers a role that can only see records it is a member of.
- **Connections:** the app needs a direct or session-mode connection for the listener (§10). Transaction-mode poolers are safe for everything else, by construction.

**Tests:**

- For every scoped aggregate and role, the rows visible through row-level security alone (query filter off) equal the rows visible through the filter. The same for child and audit rows.
- A second user on a reused pooled connection sees none of the first user's rows.
- A startup check fails if a `[RecordScoped]` table, or a child table of one, has no policy.

### 7.5 Audit

- **Capture (carried over):** an interceptor writes `audit_event` and one `audit_change` per property, with old and new values, in the same transaction. Entity attributes mark masked values, ignored columns, `*Type` codes and references to users.
- **Every event belongs to a root record:** `audit_event` stores the changed row's own type and key, and also its aggregate root's type and key. A child entity declares how to reach its root. A convention test fails for an audited entity that is neither a root nor declares one. This is what lets History show a record together with its children (§11.5), and lets row-level security hide them together (§7.4).
- **Every event says who, when and from where:** the user's id and their display name at that moment, the session, the source address, the time from the database clock in UTC, and the trace id. It also stores a short label for the changed row (a task's title), so a removed child can still be named.
- **Protection:** insert-only grants, and a trigger that rejects update, delete and truncate. The trigger has one escape: a delete by the owner role inside a transaction that has declared an audit retirement (below).
- **Tamper evidence (new):** a daily seal. A hosted job hashes each finished day's audit rows in order, chains the hash to the previous seal, stores it in `audit_seal`, and emits it as a security event so a copy leaves the database. `verify-audit` recomputes the chain and reports the first mismatch.
- **Bounded (new):** `audit-retire --before <date>` is an owner-run command. It verifies the seals, removes rows older than the date in one transaction without blocking saves, and records the retirement (date, row counts, the hash of the last retired seal) as a security event. After a retirement, `verify-audit` starts from the first surviving day and takes that recorded hash as its anchor. No partitioning **(Claude's call: partitions add a way for every save to fail if one is missing; revisit only if volume demands it)**.
- **Reading:** History follows the record's own permissions: no record access, no history; unreadable properties are left out; masked values never leave the server.
- **Security events:** sign-in, sign-out, refused access, role changes, session timeouts, masked-field reveals, system-use notice acknowledgements, audit seals and retirements. Stored in `security_event` and emitted as structured logs for a SIEM.

## 8. Identity and security

### 8.1 Sign-in and roles (carried over)

- Entra ID through OpenID Connect with PKCE. Users are keyed on tenant id and object id. Every endpoint requires a signed-in, active app user unless it opts out.
- **Anyone in the Entra tenant can sign in (Said).** The app accepts sign-ins only from its configured tenant. A first sign-in creates the user's account automatically: active, with no roles and no expiration date. Nobody has to add the user first. A department that wants to narrow who gets in does it in Entra (assignment required on the app), not in code.
- **Getting in grants nothing by itself (Said).** What a user can open, and which records they see, comes only from their roles and from record-level access (§6.4, §7.3, §7.4). A user with no roles sees the home page and My profile, and nothing else.
- **Account expiration (Said).** A user may have an expiration date, set by an Admin on the Users tab (§12). Empty means the account does not expire.
  - The account works through the end of that date. After it, sign-in is refused with a bilingual message, and the user's open sessions end at the next revalidation.
  - Moving or clearing the date restores access. A refused sign-in is a security event.
  - A rule rejects a date in the past; to stop an account today, deactivate it.
- **The last Admin is protected (Claude's call).** A rule refuses to deactivate, expire or remove the Admin role from the only active Admin, so the app cannot be locked out from its own Administration page.
- The mock is selected by configuration and refused outside Development and Testing.
- Roles come from our database, added as claims by an idempotent claims transformation, cached, and dropped the moment they change.
- The first Admin is created once by `bootstrap-admin`.
- Open circuits revalidate periodically and immediately when that user's roles, status or session change.

### 8.2 Server-side sessions (new)

- The cookie (`__Host-` prefix, Secure, HttpOnly, SameSite=Lax) holds only an opaque session key. The ticket lives in an `auth_session` row, protected with Data Protection.
- Limits enforced on the server: 20 minutes idle, 8 hours absolute. The circuit's activity monitor refreshes "last seen" while the user is really active.
- Sign-out deletes the row. Deactivating a user deletes all their sessions. Open circuits for a deleted session end at once.
- **Last sign-in and online status (Said):** sign-in stamps the user's `last_sign_in`. Each open circuit refreshes a heartbeat on its session about once a minute, whether or not the user is typing. A user is "online" when they have a session whose heartbeat is under two minutes old **(Claude's call)**. This works across servers because it lives in the database.
- The idle warning (at 18 minutes, with "Stay signed in") is carried over. **Unsaved edits are lost at timeout**, because drafts are out (R4); the warning says so.

### 8.3 Browser and request hardening

- **Headers (carried over):** strict CSP with a per-response nonce, `form-action` limited to the app and the configured sign-in provider, HSTS, `nosniff`, referrer policy, a deny-all permissions policy, same-origin opener policy. No inline `style` attributes anywhere (a test enforces it).
- **No caching (new):** every page and endpoint response carries `Cache-Control: no-store`, so protected data is not kept by a browser or proxy. Static files are the exception.
- **Antiforgery** on every plain form.
- **Rate limits (new):** a strict per-client limit on sign-in, sign-out and language endpoints, and a general per-client limit on all HTTP endpoints. SignalR and circuit limits (message size, parallel calls, retained circuits) are set explicitly in configuration.
- **Proxies (new):** forwarded-headers settings come from configuration, so limits and logs see the real client.
- **Error boundary (new):** the layout wraps page content. An unhandled component error shows a bilingual message with a reference number and a "try again" action; the session survives.
- **Errors never show internals.** The reference number is the trace id.

### 8.4 Startup checks and `doctor` (new)

Before serving, the app verifies and refuses to start on any failure, listing every finding:

- Data Protection keys are protected; the mock sign-in is not configured in Production; `AllowedHosts` is not `*` in Production.
- The database user is not the owner or a superuser, cannot create tables, and cannot update or delete audit rows.
- In Production, the database connection is encrypted.
- Every `[RecordScoped]` table has row-level security and a policy.
- No migration is pending; `*Type` seed rows match the code constants.

`dotnet CslaStarter.Web.dll doctor` prints the same report without starting the app.

### 8.5 Permissions matrix (new)

- A generated file, `docs/security/permissions-matrix.md`: for every business type, each object action and each property's read and write, by role. Rules that depend on the record are marked as such.
- A test fails when the file is stale, so every permission change shows up in a pull request diff.

### 8.6 Supply chain (carried over)

NuGet audit, CodeQL, Dependabot, an SBOM per release, and the design system vendored with pinned integrity hashes.

### 8.7 Protected B: what the starter covers and what the host must provide (Claude's call)

A template cannot be Protected B on its own. Each app built from it gets its own security assessment and authorization from the department. The starter's job is to arrive with the application-level controls built, tested and documented, and to say plainly what it leaves to the host.

**The starter provides:**

- **A control mapping:** `docs/security/control-mapping.md`, shipped in the handbook. For each ITSG-33 control family the app touches (access control, audit and accountability, identification and authentication, system and communications protection, system and information integrity, configuration management), it lists what the starter does, the file it lives in, and the test that proves it. A test fails if the mapping names a file or test that no longer exists. This is the evidence an assessor asks for.
- **No protected data in logs, traces or change notices.** They carry types, keys and outcomes, never field values. A test saves a record with marker values and fails if a marker appears in any captured log line, trace attribute or notice.
- **Audit and security events that say who, what, when, from where and with what outcome** (§7.5).
- **Standard cryptography only:** the platform's own implementations of approved algorithms (AES-256, SHA-256 or stronger). No home-made cryptography.
- **Responses that are not cached** (§8.3) and **an encrypted database connection, checked at startup** (§8.4).
- **A system-use notice** an Admin can switch on (§8.8).

**The host and the department provide** (listed in the same mapping and in the handbook's operations chapter, each with how to verify it):

- Encryption at rest for the database, its backups and the log store.
- TLS 1.2 or later with Cyber Centre approved cipher suites at the front door (ITSP.40.062).
- Hosting, backups and telemetry storage in Canada.
- Multi-factor authentication, through Entra ID conditional access.
- Network zoning, patching, time synchronisation, and tested backup and restore.
- Retention periods for audit rows and business records, and the privacy impact assessment.

### 8.8 System-use notice (Said)

A notice shown to each user after sign-in. An Admin manages it from the Administration page (§12); nothing needs a deployment.

**The "System notice" tab** (Platform, Admin only):

- An on/off switch, and the notice text in English and in French.
- It is one record. Changes are audited and the tab shows History (§11.5).
- The text is plain text with line breaks, never HTML.
- A business rule requires both languages before the notice can be switched on.

**When the notice is on:**

- After sign-in, the user sees it in their language before any other page, with two actions: Continue and Sign out. The language toggle works on this page.
- Continue records the acknowledgement on the user's session (§8.2) and as a security event: who, when, and which version of the text.
- It is asked once per session.
- It is enforced on the server: a session with no acknowledgement cannot open any other page, whatever address is typed.

**When the notice is off,** nothing is shown and nobody is asked.

**My calls:**

- A change to the switch or the text applies from each user's next sign-in. Users already signed in are not interrupted.
- The starter ships the notice switched off, with sample text in both languages. The wording is the department's to set.

**How it is built:** `Domain/Platform/Notice` holds an editable object for the Admin page and a read-only one that any signed-in user may read. The read-only one is cached and refreshed by a change notice (§10), like translations.

## 9. Localization

### 9.1 Culture (carried over)

- en-CA and fr-CA. The culture comes from a cookie, set from the user's preferred language at sign-in.
- Switching language goes through an endpoint and a full reload, because a circuit's culture is fixed when it starts. With unsaved edits, the unsaved-changes dialog asks first.

### 9.2 Translations (Said)

All interface text and labels come from one database lookup. There are no `.resx` files.

- **`TranslationType`:** `Code` (assigned by the database), `SourceString` (unique), `EnDesc`, `FrDesc`, `IsActive`, `AddedDate`.
- **Source code refers to a translation by its source string**, written as readable English:
  - a property label: `[Label("Project code")]`;
  - interface text in a component: `T("Save")`.
- **Resolution:** a singleton translator holds every row in memory and returns `EnDesc` or `FrDesc` for the current culture. `[Label]` and CSLA's `FriendlyName` read it on every access, so text follows the language and picks up edits.
- **A string with no row is added at run time:** `EnDesc` is the source string, `FrDesc` is `fr-` plus the source string (`Label` becomes `fr-Label`), `IsActive` is true, `AddedDate` is now. The insert is safe when several servers see the string at once (unique index, insert-if-missing), runs in its own short transaction as the system actor, and is audited.
- **Only literals:** the argument to `T(...)` and `[Label(...)]` must be a string literal or constant. A Roslyn test enforces it, so user input can never create rows.
- **Placeholders:** a source string may contain `{0}`-style placeholders, filled by the caller: `T("Updated by {0}", name)`.
- **Edits take effect at once:** saving a translation publishes a change notice (§10); every server reloads its cache.
- **If the database cannot be reached** (the error page, startup failures), the translator falls back to the source string in English and `fr-` plus the source string in French.
- **`IsActive`** marks a row that code no longer uses **(Claude's call)**. It is housekeeping: the page hides inactive rows by default, and resolution is unaffected. A command lists rows that no code refers to.

### 9.3 Error messages (Said)

- **`ErrorMessageType`** is a code-owned type: `Code`, `EnDesc`, `FrDesc`, `IsActive`, `AddedDate`, with a `public const int` per message (`ErrorMessageType.Required`, `ErrorMessageType.ProjectCodeFormat`).
- **Source code refers to an error message only through a constant.** This covers the rule attributes, rule classes, the exception inspector's messages (duplicate value, concurrency, not found, access denied) and the error pages.
- Messages may contain placeholders (`{0}` is the field's label for property rules).
- **"Correctly refers" is enforced by tests:** every constant has a row and every row has a constant; no code passes a number or a string where an `ErrorMessageType` constant is expected; every shipped message has real English and French; the placeholders in both languages match.
- The same cache, change notice and database-down fallback as translations. The fallback text for an error message is compiled in from the seed file.

### 9.4 How text ships between environments (Claude's call)

Rows added at run time live only in that environment's database, so the repository carries seed files:

- `Data/Seed/translations.json` (source string, English, French) and `Data/Seed/error-messages.json` (code, English, French).
- At startup the app inserts any seed row that is missing. It never overwrites a row that exists, so an Admin's edits in an environment are kept.
- `translations export` writes the rows of a development database (run-time additions and edits made on the Translations page) back into `translations.json`, ready to commit. This is how a developer's translations reach production.
- **The starter ships fully translated:** a test scans the code for every string passed to `T(...)` and `[Label(...)]` and fails if the seed file lacks it or its French starts with `fr-`. The same test ships in generated apps, so untranslated text is caught before a release, while run-time additions keep development fast.

### 9.5 Tests

- Every string used in code is in the seed file with real French (above). Every `*Type` row has both descriptions.
- Run-time addition: first use inserts exactly one row with the `fr-` French, also when two servers race.
- An edit on one server shows on another without a restart.
- No shipped page shows text starting with `fr-` in French mode (browser tests).

## 10. Live updates

- **One path, always PostgreSQL** **(Claude's call)**: the commit issues `pg_notify` (§7.2); a hosted listener on a dedicated connection receives it and dispatches to an in-process hub. The same code runs for one server and for many. There is no separate in-memory mode.
- **A notice carries** type, key, kind (created, updated, deleted, access changed), version, who changed it, and the originating circuit. Never field data.
- **The hub** keeps subscribers per topic: `(type, key)` for an open form, `(type)` for a list. Dispatch touches only that topic's subscribers. One subscription per form or table; fields never subscribe.
- **Reconnect:** the listener reconnects with backoff and then raises a resync, so every list refreshes and every form checks its version. The listener's connection must be direct or session-mode, never a transaction-mode pooler; `/health/ready` reports whether it is connected.
- **Notices are hints.** Optimistic concurrency is the guarantee.
- **A list** refreshes quietly (debounced), keeping sort, page and filters.
- **A form with no edits** reloads and announces "Updated by Jane".
- **A form with edits** shows a banner: "Jane changed this record. Reload (your edits will be lost)." Saving anyway fails the version check and shows the same choice. There is no merge.
- **Losing access** to an open record closes the form with a message.

## 11. User interface

The interface is part of every slice, not a later phase.

### 11.1 Shell (GC Design System)

- Header with the Government of Canada signature, language toggle and skip link; the app name; navigation that shows only what the user may open; the user's name and a sign-out control.
- Breadcrumbs, a page heading that also sets the document title, a main container, a compact footer, and the date-modified line with the build version.
- Bilingual pages for: home, access denied, not found, error, signed out. The Blazor reconnection notice is restyled and bilingual.
- One small stylesheet that uses design-system tokens only. No custom visual identity.

### 11.2 Component kit (`Web/Platform/Components`)

Every component is `X.razor` (markup) plus `X.razor.cs` (code) and inherits `CslaBaseComponent`, which provides: a stable id, a disposal token, `RunAsync` (busy state, tracing, error handling), toasts and screen-reader announcements, change subscriptions that end with the component, the current user, and `T("…")` for interface text (§9.2).

| Component | Does |
|---|---|
| `CslaForm<T>` | Loads through a portal delegate, owns the view model, validates, saves, cancels, deletes after confirmation, guards unsaved changes, shows the error summary and moves focus, reacts to live updates (§10) |
| Fields | Text, text area, number, date, select (including `*Type` lists), checkbox, radio group, sensitive (masked on the server, audited reveal). Each takes `For="() => form.Model.X"` and reads label, value, errors, warnings, information, busy, read and write permission from CSLA's `Csla.Blazor.IPropertyInfo`. |
| `CslaTable` | Server paging, sorting and filtering; keyboard row selection; live refresh |
| `CslaButton` | Save, cancel, delete, new, custom; follows permissions, `IsSavable` and busy state |
| `CslaHistory` | A record's changes: date, changed by, field, old and new value, covering its children (§11.5) |
| Others | Tabs (each tab a real address), error summary, toast host, live region, change banner, confirm dialog (a real modal), session-timeout warning, language toggle, `CslaAuthorize` |

`AppViewModel<T>` is a thin subclass of CSLA's `ViewModel<T>`.

- **Cancel reloads** **(Claude's call)**: it runs the form's load delegate again and replaces the object. It is always correct, including after a failed save, and it does not depend on undo levels. `ManageObjectLifetime` stays off.
- **Save failures are never swallowed:** CSLA's view model captures exceptions instead of throwing; the form reads them and routes them through `RunAsync`.
- A dialog that edits one child row may use CSLA's `BeginEdit` and `CancelEdit` on that child, which is what they are for.

### 11.3 Design-system interop (carried over, already proven)

- Custom events registered in a JS initializer under names that differ from the browser's.
- Every field sets `validate-on="other"`, and forms set `novalidate`: CSLA is the only validator.
- Never bind a changing `class` on a design-system element; object properties pass as JSON; every field host has an `id`.
- The design system's nonce comes from the page's `csp-nonce` meta tag.

### 11.4 Accessibility

- axe (WCAG 2.1 AA) scans every page in both languages in the browser tests.
- Browser tests also cover: completing a form by keyboard only, focus moving into and out of dialogs, and the error summary receiving focus.

### 11.5 Record history (Said)

One component, `CslaHistory`, shows what changed on a record. It reads the audit rows (§7.5); nothing is stored a second time.

**What it shows.** A table, newest first, paged on the server:

| Column | Content |
|---|---|
| Date | When the change was saved, in the app's configured time zone |
| Changed by | The user's display name at the time of the change; "System" for system work |
| Item | Shown when children are included: the record itself, or the child row, for example "Task: Write the report" |
| Field | The property's label, in the current language |
| Old value, New value | Formatted for reading (below) |

Changes saved together are grouped under one date and name. A child row that was added shows as "added" with its first values. One that was removed shows as "removed" with its last values.

**Children.** The history of a root covers every child and grandchild row, because each audit event carries its root's key (§7.5). The same component gives three views:

- a whole record with its children: `<CslaHistory For="form.Model" />`
- one field: `<CslaHistory For="form.Model" Property="() => form.Model.Budget" />`
- one child row: `<CslaHistory For="task" />`

A filter narrows the table to one item or one field.

**Every form has it.** `CslaForm` shows a History action whenever the business class carries `[HistoryOf]`. Pages write no code for it **(Claude's call)**. The component can also be placed on its own.

**Values read the way the form shows them:**

- a `*Type` code shows its description; a reference to a user shows the user's name;
- dates, numbers and yes/no follow the current language; an empty value shows as "(empty)";
- a masked field says that it changed and never shows either value;
- labels and descriptions are today's text in the current language, not the text at the time of the change.

**Permissions are the record's own:**

- A user who cannot open the record gets "not found", never its history.
- A property the user may not read is left out, and so are its entries in counts and paging, so nothing can be inferred. Child rows follow the child class's rules.
- This is enforced on the server in the history business object, and again by row-level security on the audit tables (§7.4).

**How it is built:**

- `Domain/Platform/History` holds a read-only paged list, fetched by record type and key, with an optional child or property. It matches audit columns to business properties by name. A convention test fails when an audited column has neither a matching business property nor an "ignored" mark, so no change can go unshown by accident.
- The component is a design-system table with a caption and proper headers. It refreshes when the record's change notice arrives (§10).
- A deleted record keeps its audit rows, but this version has no screen to browse them (§18).

## 12. Features shipped

| Feature | Demonstrates |
|---|---|
| **Users** (Administration tab) | A paged, filtered list showing each user's roles, status, expiration date, last sign-in and whether they are online now (the online column refreshes while the page is open). An editable root with a role-assignment child list; a `*Type` dropdown; a masked sensitive field; role changes that take effect in open sessions. |
| **My profile** (Platform) | Editing only your own record; preferred language |
| **Translations** (Administration tab) | A paged list with search and filters: not translated (French starts with `fr-`), active or inactive. Editing `EnDesc`, `FrDesc` and `IsActive` only. Audited, with history. Edits show at once on every server. |
| **Error messages** (Administration tab) | A paged list with search, showing each message's code. Editing `EnDesc`, `FrDesc` and `IsActive` only. Audited, with history. |
| **System notice** (Administration tab) | One record: an on/off switch and the sign-in notice text in English and French (§8.8). Audited, with history. |
| **Projects** (sample) | A record-scoped list and editable root with task and member child lists; a command (close project); an async rule (unique code); property permissions on Budget; history per record and per field |

**The Administration page (Said)** is the one place to manage everything about users, translations, error messages and the system-use notice. It is one navigation entry with a tab for each:

| Tab | What an Admin does there |
|---|---|
| Users | Finds a user in the list (roles, status, expiration date, last sign-in, online now), with a filter on status: active, inactive, expired. Opens a user to assign or remove roles, set or clear the expiration date (§8.1), activate or deactivate the account, end the user's open sessions **(Claude's call)**, and read the user's history. Users are never added here: an account appears at its first sign-in. |
| Translations | Searches and filters (not translated, active, inactive). Edits the English and French text and `IsActive`. Reads each row's history. |
| Error messages | Searches by text or code. Edits the English and French text and `IsActive`. Reads each row's history. |
| System notice | Switches the sign-in notice on or off and edits its text (§8.8). |

- **Admins only, by default.** The page and each tab follow the permissions of the business objects behind them, and the starter's rules allow only the Admin role. An app that wants another role to manage translations, for example, changes that one rule; the tab and the navigation entry follow. A user who may open no tab does not see the entry, and gets "access denied" at its address.
- Each tab has its own address, so a link, the back button and a reload all land on the same tab. A user record opens inside the Users tab, at its own address.
- Leaving a tab with unsaved edits asks first.
- A new tab is a page with one attribute, found by convention like navigation (§5). No existing file is edited. This is where an app puts its own admin options.
- The tabs are keyboard-operable and scanned by axe in both languages.

- Every edit page above shows History (§11.5). On Users it includes role assignments, which are child rows: "Role added: Technical".
- Rows of `TranslationType` and `ErrorMessageType` are created only by code (seed files and run-time addition). The pages offer no add or delete.
- **Sample permissions (carried over):**
  - Admin and Technical see every project; Client sees only projects they are a member of, read-only.
  - Admin and Technical create, edit and close projects; only Admin deletes them and manages members.
  - Budget: Admin and Technical read it; only Admin changes it; Client does not see it.
- The sample lives in `Features/Projects` in each project with its own migration. A tested script removes it.

## 13. Template, scaffolding and starter updates

### 13.1 Template (carried over)

`dotnet new csla-blazor -n MyApp [--claude true|false]`, packed from the `claude` branch. Hyphenated names work. Without `--claude`, Claude files and the sync workflow are left out and workflows target `main`.

### 13.2 Scaffolding (new)

Item templates in the same package, usable without Claude:

- `dotnet new csla-feature -n Contract`: business root, list and row, DAL contract and DTOs, entity, configuration, DAL, list and edit pages, and their tests.
- `dotnet new csla-type -n Priority`: the type class, its entity and seed configuration.

Because registration and navigation are by convention (§5), the scaffold edits no existing source file. The developer then adds a migration (which updates the model snapshot) and regenerates the permissions matrix. Labels need no step: new source strings are added at run time (§9.2) and exported to the seed file before release (§9.4).

The Claude skills call the same templates and then fill in the specifics.

### 13.3 Taking starter updates (new)

- The `Platform` folder of each project is starter-owned, including the component kit and the core pages. `PLATFORM_VERSION` records which starter release an app was made from.
- `scripts/platform-diff.sh <path-to-newer-starter>` lists what changed in `Platform` since, so one developer can bring a fix to each app deliberately.

## 14. Hosting and observability (carried over, with additions)

- IIS (in-process, WebSockets on, affinity for several servers) or App Service (WebSockets on, Always On, affinity).
- `/health/live` and `/health/ready` (database, listener connected).
- OpenTelemetry traces, metrics and logs over OTLP, including CSLA's data portal metrics. The trace id is the user's reference number and the audit correlation id.
- The handbook's operations chapter covers deployment on both hosts, database roles, backup and restore, the owner commands, and upgrades.

## 15. Testing

| Layer | Covers |
|---|---|
| Domain | Every rule; every role × action × property; the data portal flow, with fake DALs (`Csla.Testing`) |
| Conventions (free for every business type) | Class order; every property has a label; object-level rules exist; a created instance clones and round-trips; the permissions matrix is current; code-owned type constants match their rows; the text checks in §9.3 to §9.5 |
| Web (bUnit) | What each component and page shows per role and state |
| Integration | DALs on real PostgreSQL; unit of work atomicity (change, audit and notice together); version conflicts; row-level security matrix; sessions; rate limits; headers; startup checks; a child's change is stored against its root; history per role leaves out unreadable properties and hidden records; no field value reaches a log, trace or notice |
| End to end | Real sign-in and sign-out; every page in both languages with axe; create, edit, delete; two browsers and a live update; record-level access per role; session timeout; keyboard-only form; edit a field and a child row, then read both in History with the old value, new value, date and name; an Admin switches the system-use notice on, the next sign-in must acknowledge it before any page opens, and switching it off removes it; a tenant user who has never signed in gets in, has no roles, and sees only the home page and My profile; an expired user is refused at sign-in; a non-Admin cannot open the Administration page |
| Architecture | Boundaries (§5), no inline styles, `.razor` pairs, documentation links, template output |

- Test-driven throughout: a failing test first, for every behaviour.
- **End-to-end tests run on every pull request**, not only after merge.
- Line coverage gate of 80% on `Domain` and on `Web/Platform/Components`.

## 16. Repository, CI and documentation

- **Branch:** all rebuild work happens on one branch cut from `claude`. It merges into `claude` only when the whole rebuild is complete and Said approves. `claude` and `main` keep the first build until then.
- **What is kept from the repository** **(Claude's call)**: the plumbing that is proven and unrelated to the CSLA design: workflows, the `claude` → `main` sync and guard, the coverage and design-system scripts with their tests, database role scripts, docker-compose and the mock configuration, the vendored design system, build settings, and the template project. Each is adapted to the new project names.
- **What is rewritten:** everything under `src/` and `tests/`, written test-first against this design. Proven code from the first build (security headers, audit capture, claims transformation, design-system interop) may be ported when a new test covers it.
- **Handbook (new):** `docs/handbook/`, for a human, shipped to `main` and to generated apps: getting started, architecture, business objects, data, security, components, live updates, testing, operations, recipes (field, type, feature, page, remove the sample), and short decision records that say why.
- **Claude guidance (Said, 2026-11-17):** see §16.1. Recipes are still written once, in the handbook, so a human and Claude follow the same steps.
- The interactive architecture page in `docs/architecture/` is updated to match at the end.
- Jira: Story DEV-39 under epic DEV-37. Commits start with `DEV-39`. The branch is `DEV-39-starter-rebuild`.

### 16.1 Claude guidance (Said)

A new Claude session must be able to start in this repository, or in an app made from it, and work reliably without being told how.

**Files:**

- **Root `CLAUDE.md`** is the entry point: what this is, the commands, the project map, the golden rules, how to start a session, and an index that says which file to read for which job.
- **One `CLAUDE.md` in every project,** `src` and `tests`. It holds what Claude needs to work in that project: what lives there, each pattern with a real file to copy from, what must never be done there, how to test, and the known pitfalls.
- **Guides in `docs/claude/`,** reached from the root index:
  - the working agreement: test first, the commands that prove a change, the definition of done, when to stop and ask;
  - the business rules guide (below);
  - a job index that points at the handbook recipe for each job: add a field, a rule, a permission, a type, a feature, a page, a translation, an error message.
- Claude files never restate a recipe. They add only what is specific to Claude **(Claude's call)**.

**Custom business rules get special care:**

- Before writing a rule, Claude asks the developer until every item of a fixed checklist is answered: what triggers the rule; which properties it reads and which it affects; error, warning or information; the message in English and French; whether it needs data from the database; whether it applies to new records, existing ones or both; what happens for empty, null and boundary values; how it meets permissions; and at least one example that passes and one that fails. Claude does not guess an answer.
- The agreed outcomes are written as a table at the top of the rule's test file, and there is one test for each row.
- Enforced by tests: every rule class and rule attribute lives in a `Rules` folder and has a test class; line and branch coverage of everything in a `Rules` folder must be 100%.

**Kept current, checked after every task:**

- **Tests validate the files:** every project has a `CLAUDE.md` with the required sections; every link resolves; every repository path, command, project and type that a Claude file or handbook page names exists; the root index reaches every guide.
- **The definition of done** in the working agreement includes updating the files a change affects.
- **A `docs-check` skill** compares a change with the files that cover the changed code and reports anything now false or missing.
- **Every task in every plan ends with a documentation step,** and the task's review checks it.

All of this ships with `--claude true`, so a generated app has the same system from its first commit.

## 17. Delivery: slices

Each slice ends with its tests green, including a real-browser test, and a short demonstration.

| # | Slice | Proves |
|---|---|---|
| 0 | Skeleton: three projects, build, CI on the branch | The structure and boundaries compile and are enforced |
| 1 | **Walking skeleton:** mock sign-in, the layout shell, one Users list and edit screen with one field, saved to PostgreSQL with audit, in both languages from `TranslationType` and `ErrorMessageType`, with axe | The generator, attribute rules, database-backed labels and messages, unit of work, the kit's core, and the browser path all work together |
| 2 | Security core: roles, sessions, timeout, headers, rate limits, startup checks, `bootstrap-admin`, security events. The change hub and listener are built here, because role and session changes must reach open circuits on every server. | §8, and the transport half of §10 |
| 3 | Business platform: hooks, types, children, concurrency; My profile complete; the Administration page with its Users (roles, expiration date, last sign-in, online), Translations, Error messages and System notice tabs, and the sign-in acknowledgement; seed files and export | §6, §8.8, §9, §12 |
| 4 | Record-level access with row-level security; the Projects sample | §7.3, §7.4, §12 |
| 5 | The History component (record, field, children) on every form; seals, retirement, permissions matrix, control mapping | §7.5, §8.5, §8.7, §11.5 |
| 6 | Live updates in the interface: list refresh, form reload and banner, losing access | The rest of §10 |
| 7 | Kit and interface complete: every component, page and state, both languages, accessibility | §11 |
| 8 | Template, scaffolding, handbook, skills, release | §13, §16 |

Each slice gets its own implementation plan, written after the previous slice.

## 18. Not in this version

Listed so they are choices, not omissions: drafts and autosave; presence; field-by-field merge; keyboard shortcuts; changed-field highlighting after a reload; a screen for browsing the history of deleted records; a remote data portal; shared packages; audit partitioning.

Declined by Said on 2026-11-17, so an app that needs one adds it itself: logging who viewed a record; encrypting individual columns; automatically deactivating accounts that have not been used.

## 19. Left to the builder

- Final names of attributes, internal classes and files, within the conventions above.
- How the new row version returns from the commit to the root object.
- The shape of the notice payload and of the transaction-local settings used by row-level security.
- How the creating transaction is allowed to see a new root under row-level security (§7.4).
- How pages declare their navigation entry.
- How a child entity declares its root and its label, and how History turns a stored value into display text.
- Rate-limit, debounce and revalidation values within sensible ranges, all configurable.
- The architecture-test and hashing libraries.

## 20. Risks, and what slice 1 must prove

| Risk | Answer |
|---|---|
| The property generator misbehaves with our base classes, nullable types or attributes | Slice 1 proves it. Fallback: hand-written properties in the same order; nothing else changes. |
| Attribute rules and labels don't pick up database text as expected | The hooks are verified in source (§6.1); slice 1 proves it in a browser. Fallback: CSLA common rules with `Func<string>` messages reading the same lookup. |
| Labels and messages need the database, and attributes cannot use dependency injection | One singleton translator with an in-memory cache, reached through a static accessor set at startup; the database-down fallback in §9.2; tests run with a default translator that returns the source string |
| French users see `fr-` placeholders in production | The seed-file test (§9.4) fails the build for any string used in code that lacks real French |
| Two definitions of record access (filter and policy) drift | The equality test in §7.4 |
| A user's database settings reach another user's query | Settings are transaction-local (§7.2); the reuse test in §7.4 |
| A role that sees records only through membership cannot create one under row-level security | The policy pattern and test in §7.4 |
| History shows a value the user may not read on the form | History applies the business class's own read rules, per property and per child class; a test runs it for every role (§11.5) |
| A daily seal raises a false alarm for a late commit | A grace window before sealing; `verify-audit` reports, it does not block |
| Blazor Server is stateful: affinity, memory per user, edits lost on recycle or timeout | Documented sizing and hosting; the timeout warning; drafts can return later |
| One developer, niche framework | The handbook, scaffolding, convention tests, and staying close to CSLA's documented patterns |
