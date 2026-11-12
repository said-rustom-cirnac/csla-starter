# Plan 2: Data and Security Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A PostgreSQL data layer with an audit trail and a security event log. On top of it:

- Entra ID sign-in, with a mock OpenID Connect server for development and tests
- roles from the database and the `bootstrap-admin` command
- security headers with a nonce-based CSP, en-CA/fr-CA localization plumbing, and the idle-timeout service
- health checks and telemetry

Everything is proven by tests against real PostgreSQL.

**Architecture:**

| Project | Gets |
|---|---|
| Core | Contracts (`IDataAccessContext`, `ISecurityEventLog`, `IAuditHistoryReader`) and `.resx` resources with `LocalizedDisplayNameAttribute` |
| Data | `AppDbContext` (Npgsql, snake_case, Guid v7 keys, `xmin` on roots), the audit interceptor, append-only protection, the audit reader, the security event log and the Data Protection key store |
| Infrastructure | Sign-in plumbing (auth configuration, claims transformation, user directory, bootstrap-admin), Data Protection setup, localization, idle timeout, telemetry and health checks |
| Web | Composes everything, plus security headers and the culture endpoint |

**Tech Stack:**

- EF Core 10.0.15, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, EFCore.NamingConventions 10.0.1
- Microsoft.Identity.Web 4.18.0 and Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.15
- OpenTelemetry 1.21.0
- Testcontainers.PostgreSql 4.16.0 (`postgres:18`), Microsoft.AspNetCore.Mvc.Testing 10.0.15, Microsoft.Extensions.TimeProvider.Testing 10.11.0

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md`, sections 7, 8, 9, 9.1 (record filter comes in plan 3), 10, 14, 15 and 16.

## Global Constraints

- **Database:**
  - Table and column names are snake_case.
  - Primary keys are `Guid` generated in the app with `Guid.CreateVersion7()`.
  - Aggregate roots map `[Timestamp] public uint Version` to PostgreSQL `xmin`.
- **Database roles:**
  - The app connects as `app_runtime`; migrations run as `app_owner`.
  - `app_runtime` has INSERT + SELECT only on `audit_event`, `audit_change` and `security_event`.
  - A trigger blocks UPDATE, DELETE and TRUNCATE on those tables for everyone.
- **Connection strings:** `ConnectionStrings:App` (runtime) and `ConnectionStrings:Owner` (migrations, bootstrap). Never put secrets in committed `appsettings*.json`.
- **Cookie:** name `__Host-CslaStarter`, `Secure` always, `HttpOnly`, `SameSite=Lax`, 8-hour absolute lifetime, `SlidingExpiration=false`.
- **Idle timeout:** warning after 18 minutes, sign-out after 20 minutes. Both are configurable under `Session:WarningAfter` and `Session:SignOutAfter`.
- **CSP** (exact, nonce per request):

  ```
  default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'nonce-{nonce}'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; form-action 'self' https://login.microsoftonline.com; upgrade-insecure-requests
  ```
- **Other headers:** `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()`, `Cross-Origin-Opener-Policy: same-origin`. HSTS outside Development.
- **Cultures:** `en-CA` (default) and `fr-CA`, chosen by the culture cookie. `GET /culture/set?culture=&redirectUri=` redirects to local URLs only.
- **Mock OIDC:** `Authentication:Mode = MockOidc` is refused in the Production environment, and the app fails to start.
- **Audit:** captured in the same transaction as the change. Values are stored as invariant-culture strings. `[AuditMask]` stores no values. `Version`, `CreatedOn/By` and `ModifiedOn/By` are never audited.
- **Package versions:** every new package gets its version in `Directory.Packages.props` only. Microsoft.* stays on 10.0.x.

## Review Focus

1. **An entity marked Modified whose values didn't actually change must write no audit event.** EF marks whole entities Modified after an attach. *Pinned by* `Audit_UpdateWithNoRealChange_WritesNothing` (Task 3).
2. **A delete must record the old values, and an insert the new ones.** The history has to show what was removed. *Pinned by* `Audit_Delete_RecordsOldValues` and `Audit_Insert_RecordsNewValues` (Task 3).
3. **`/culture/set` with `redirectUri=https://evil.example` or `//evil.example` must not redirect off-site.** *Pinned by* `CultureSet_RejectsOffSiteRedirect` (Task 6).
4. **A deactivated user (`is_active = false`) still has a valid Entra session, but must get no role claims.** *Pinned by* `ClaimsTransformation_InactiveUser_GetsNoRoles` (Task 5).
5. **Running `bootstrap-admin` a second time must refuse, with exit code 2 and no new rows.** *Pinned by* `BootstrapAdmin_RefusesWhenAnAdminExists` (Task 5).

---

### Task 1: Core contracts and localization resources

**Files:**
- Create in `src/CslaStarter.Core/`:
  - `Security/IDataAccessContext.cs`, `Security/ISecurityEventLog.cs`, `Security/SecurityEvent.cs`, `Security/SecurityEventKind.cs`
  - `Auditing/IAuditHistoryReader.cs`, `Auditing/AuditModels.cs`
  - `Localization/SupportedCultures.cs`, `Localization/LocalizedDisplayNameAttribute.cs`
  - `Resources/Labels.resx`, `Labels.fr.resx`, `Messages.resx`, `Messages.fr.resx`
- Create: `tests/CslaStarter.Business.Tests/` (references Business; `xunit.v3`), `LocalizationTests.cs`
- Create: `tests/CslaStarter.Architecture.Tests/ResourceTests.cs`

**Interfaces (Produces):**

```csharp
public interface IDataAccessContext { Guid? UserId { get; } string UserName { get; } string? CorrelationId { get; } string? OriginId { get; } }
public enum SecurityEventKind { SignIn, SignOut, AccessDenied, RoleChanged, SessionTimeout, SensitiveReveal, AdminBootstrapped }
public sealed record SecurityEvent(SecurityEventKind Kind, Guid? UserId, string? UserName, string Detail, string? CorrelationId);
public interface ISecurityEventLog { Task WriteAsync(SecurityEvent securityEvent, CancellationToken cancellationToken); }
public sealed record AuditQuery(string EntityType, string EntityKey, string? Property = null, int Page = 1, int PageSize = 50);
public sealed record AuditChange(string Property, string? OldValue, string? NewValue, bool IsMasked, string? TypeCodeOf);
public sealed record AuditEntry(long Id, DateTimeOffset OccurredAt, Guid? UserId, string UserName, string Action, string EntityType, string EntityKey, IReadOnlyList<AuditChange> Changes);
public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, int TotalCount);
public interface IAuditHistoryReader { Task<AuditPage> GetAsync(AuditQuery query, CancellationToken cancellationToken); }
public static class SupportedCultures { public const string English = "en-CA"; public const string French = "fr-CA"; public static IReadOnlyList<string> All { get; } }
public sealed class LocalizedDisplayNameAttribute(string key) : DisplayNameAttribute { public string Key { get; } public override string DisplayName { get; } }
```

- `LocalizedDisplayName` resolves `Labels.ResourceManager.GetString(Key, CultureInfo.CurrentUICulture)` and falls back to `Key`.
- `Labels` and `Messages` are public strongly-typed resource classes in `CslaStarter.Core.Resources`, generated at build. The seed keys are `Common_Save`, `Common_Cancel`, `Common_Delete`, `Common_New`, `Common_Required`, `Error_Reference`, `UserRole_Admin` and `Language_Toggle`.

- [ ] **Step 1: Write the failing tests.**
  - `LocalizationTests`:
    - `LocalizedDisplayName_FollowsCurrentUICulture`: under fr-CA, `new LocalizedDisplayNameAttribute("Common_Save").DisplayName == "Enregistrer"`; under en-CA it is `"Save"`.
    - `LocalizedDisplayName_UnknownKey_ReturnsKey`
  - `ResourceTests.Resources_HaveEveryKeyInBothLanguages`: every `src/**/Resources/*.resx` without a culture suffix has a `.fr.resx` with the same key set and non-empty values. Read them with `XDocument`; the failure lists `<file>: <key>`.
  - `ResourceTests.Resources_AreNotEmpty`: `Labels.resx` has at least one key.
- [ ] **Step 2: Run the tests.** Run `dotnet test`. Expected: compile failure, because the types are missing.
- [ ] **Step 3: Implement the contracts, the attribute and the resources.**
  - Strongly-typed generation: `<EmbeddedResource Update="Resources\Labels.resx" StronglyTypedLanguage="CSharp" StronglyTypedClassName="Labels" StronglyTypedNamespace="CslaStarter.Core.Resources" StronglyTypedFileName="$(IntermediateOutputPath)Labels.Designer.cs" PublicClass="true" />`, and the same for `Messages`.
  - If the SDK ignores that metadata, use a hand-written `ResourceManager` accessor class with one `const string` per key, and record a ruling.
- [ ] **Step 4: Run the tests.** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5: Commit** with the message `DEV-38 Add Core contracts and bilingual resources`.

### Task 2: AppDbContext, core tables, database roles, Integration.Tests

**Files:**
- Create in `src/CslaStarter.Data/`:
  - `AppDbContext.cs`, `AppDbContextFactory.cs` (design time)
  - `Entities/{AppUser,UserRole,UserRoleTypeRow,AuditEventRow,AuditChangeRow,SecurityEventRow}.cs`
  - `Entities/IAuditedEntity.cs`, `Entities/IAggregateRoot.cs`, `Configurations/*.cs`
  - `Migrations/*_InitialCore.cs`
  - `DataServiceCollectionExtensions.cs`
- Create: `deploy/sql/create-roles.sql`, `.config/dotnet-tools.json` (`dotnet-ef` 10.0.15)
- Create in `tests/CslaStarter.Integration.Tests/`: `PostgresFixture.cs` (assembly fixture), `TestDatabase.cs`, `FakeDataAccessContext.cs`, `SchemaTests.cs`

**Interfaces (Produces):**

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{ DbSet<AppUser> Users; DbSet<UserRole> UserRoles; DbSet<UserRoleTypeRow> UserRoleTypes; DbSet<AuditEventRow> AuditEvents; DbSet<AuditChangeRow> AuditChanges; DbSet<SecurityEventRow> SecurityEvents; DbSet<DataProtectionKey> DataProtectionKeys; }
public static IServiceCollection AddCslaStarterData(this IServiceCollection services, string connectionString);   // registers AppDbContext + interceptors
// AppUser: Id, TenantId(64), ObjectId(64), DisplayName(200), Email(320)?, PreferredLanguage(5)="en-CA", IsActive=true, EmployeeNumber(20)? [AuditMask], audit stamps, Version (xmin); unique (tenant_id, object_id)
// UserRole: (UserId, RoleTypeCode) PK; FKs to app_user and user_role_type
// UserRoleTypeRow: Code PK, DescriptionEn(100), DescriptionFr(100), IsActive, SortOrder; seeded 1 Admin/Administrateur, 2 Client/Client, 3 Technical/Technique
```

`TestDatabase` (Integration.Tests) is created per test class. It runs `CREATE DATABASE test_<n> OWNER app_owner`, migrates as `app_owner`, and exposes `OwnerConnectionString`, `RuntimeConnectionString` and `AppDbContext CreateContext(IDataAccessContext? ctx = null, TimeProvider? time = null, bool asOwner = false)`.

- [ ] **Step 1: Write the failing tests** in `SchemaTests`:
  - `Migrations_CreateCoreTables`: `information_schema.tables` contains `app_user`, `user_role`, `user_role_type`, `audit_event`, `audit_change`, `security_event`, `data_protection_keys`.
  - `UserRoleTypes_AreSeeded`: codes 1, 2 and 3, with "Administrateur" for code 1.
  - `Concurrency_StaleVersion_Throws`: load the same `AppUser` in two contexts, save the first, and the second save throws `DbUpdateConcurrencyException`.
  - `RuntimeRole_CannotAlterSchema`: `CREATE TABLE x(i int)` as runtime throws `PostgresException` with `SqlState == "42501"`.
- [ ] **Step 2: Run the tests.** Expected: FAIL, because `AppDbContext` is missing.
- [ ] **Step 3: Write `create-roles.sql`.**
  - It takes psql variables `owner_password` and `runtime_password`.
  - It creates the `app_owner` and `app_runtime` LOGIN roles, and runs `ALTER DEFAULT PRIVILEGES FOR ROLE app_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_runtime` and `... GRANT USAGE, SELECT ON SEQUENCES TO app_runtime`.
  - It is idempotent (`DO $$ ... IF NOT EXISTS`).
  - `PostgresFixture` runs it as the superuser with fixed test passwords.
- [ ] **Step 4: Implement the entities, configurations and `AddCslaStarterData`, then generate the migration.**
  - Run `dotnet tool restore && dotnet ef migrations add InitialCore -p src/CslaStarter.Data -s src/CslaStarter.Data`. The design-time factory reads `CSLASTARTER_DESIGN_CONNECTION` and falls back to a placeholder local string.
  - `AddCslaStarterData` calls `UseNpgsql(cs).UseSnakeCaseNamingConvention()`.
- [ ] **Step 5: Run the tests.** Expected: PASS.
- [ ] **Step 6: Commit** with the message `DEV-38 Add AppDbContext, core tables, database roles and PostgreSQL test fixture`.

### Task 3: Audit interceptor, append-only protection and history reader

**Files:**
- Create in `src/CslaStarter.Data/Auditing/`:
  - `AuditSaveChangesInterceptor.cs`
  - `AuditAttributes.cs` (`[AuditEntity("Name")]`, `[AuditMask]`, `[AuditIgnore]`, `[AuditTypeCode("UserRoleType")]`, and `IHasAuditRoot { string AuditRootType { get; } string AuditRootKey { get; } }`)
  - `AuditHistoryReader.cs`
- Create: `src/CslaStarter.Data/Migrations/*_AuditAppendOnly.cs`
- Test: `tests/CslaStarter.Integration.Tests/AuditTests.cs`

**Interfaces:**
- Consumes: `IDataAccessContext` and `TimeProvider` (from DI), and the entities from Task 2.
- Produces: `AuditSaveChangesInterceptor : SaveChangesInterceptor` (registered by `AddCslaStarterData`) and `AuditHistoryReader : IAuditHistoryReader`.

- [ ] **Step 1: Write the failing tests** in `AuditTests`:
  - `Audit_Insert_RecordsNewValues`: inserting an `AppUser` writes an `Insert` event with `display_name` (old `null`, new `"Ada"`).
  - `Audit_Update_RecordsOnlyChangedProperties`: changing `DisplayName` writes 1 change, `"Ada"` → `"Ada L."`.
  - `Audit_UpdateWithNoRealChange_WritesNothing`: attach the entity, mark it Modified, change nothing, save. No new `audit_event`.
  - `Audit_Delete_RecordsOldValues`: every audited property has `NewValue == null` and the old value set.
  - `Audit_MaskedProperty_StoresNoValues`: changing `EmployeeNumber` gives `IsMasked == true`, with both values null.
  - `Audit_StampsCreatedAndModified`: with `FakeTimeProvider` at 2026-11-12T10:00Z and context user "tester", `CreatedOn == ModifiedOn ==` that instant and `CreatedBy == "tester"`.
  - `Audit_TechnicalColumnsAreNeverAudited`: no change rows for `version`, `created_on`, `created_by`, `modified_on`, `modified_by`.
  - `Audit_RolledBackTransaction_LeavesNoAuditRows`
  - `AuditTables_RejectUpdateAndDelete_EvenForOwner`: an UPDATE as owner throws `PostgresException` with the message containing `"append-only"`.
  - `RuntimeRole_CannotUpdateAuditRows`: an UPDATE as runtime throws `SqlState "42501"`.
  - `AuditHistoryReader_ReturnsNewestFirst_AndFiltersByProperty`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - **In `SavingChanges(Async)`:**
    1. Stamp `IAuditedEntity`.
    2. For each Added/Modified/Deleted entry whose CLR type isn't one of the audit, security, Data Protection or `[AuditIgnore]` types, build an `AuditEventRow` with its changes:
       - Modified: only properties where `IsModified && !Equals(OriginalValue, CurrentValue)`.
       - Added: old `null`. Deleted: new `null`.
    3. Skip an event with no changes, and add the rows to the same context.
  - **Values:** `Convert.ToString(v, CultureInfo.InvariantCulture)`, with `DateTimeOffset` formatted as `"O"`.
  - **Key:** the primary key value(s), joined with `"|"`. The root comes from `IHasAuditRoot`, or else the entity itself.
  - **Migration `AuditAppendOnly`:**
    - plpgsql function `audit_block_mutation()` raises `'% is append-only', TG_TABLE_NAME`;
    - `BEFORE UPDATE OR DELETE ... FOR EACH ROW` and `BEFORE TRUNCATE ... FOR EACH STATEMENT` triggers on the three tables;
    - `DO $$ IF EXISTS (role app_runtime) THEN REVOKE UPDATE, DELETE, TRUNCATE ON audit_event, audit_change, security_event FROM app_runtime $$`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add audit capture, append-only audit tables and history reader`.

### Task 4: Security event log and Data Protection key store

**Files:**
- Create: `src/CslaStarter.Data/Security/SecurityEventLog.cs`
- Create: `src/CslaStarter.Infrastructure/DataProtection/DataProtectionSetup.cs`
- Test: `tests/CslaStarter.Integration.Tests/SecurityEventLogTests.cs`, `DataProtectionTests.cs`

**Interfaces (Produces):**
- `SecurityEventLog : ISecurityEventLog`: writes a `security_event` row (kind, occurred_at, user_id, user_name, detail, correlation_id), plus an `ILogger` entry with `EventId` = (int)kind.
- `public static IServiceCollection AddCslaStarterDataProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)`
  - Always: `SetApplicationName("CslaStarter")` and `PersistKeysToDbContext<AppDbContext>()`.
  - `DataProtection:CertificateThumbprint` → `ProtectKeysWithCertificate`.
  - `DataProtection:KeyVaultKeyId` → `ProtectKeysWithAzureKeyVault(new Uri(id), new DefaultAzureCredential())`.
  - Outside Development and Testing with neither set → throws `InvalidOperationException("Data Protection keys must be protected at rest: set DataProtection:CertificateThumbprint or DataProtection:KeyVaultKeyId.")`.

- [ ] **Step 1: Write the failing tests.**
  - `SecurityEventLog_WritesRow`
  - `DataProtection_KeysPersistToDatabase`: two separate service providers share the database. Text protected by one is unprotected by the other.
  - `DataProtection_Production_WithoutKeyProtection_Throws`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** Add `Azure.Identity` and `Azure.Extensions.AspNetCore.DataProtection.Keys` to Infrastructure, with the versions in `Directory.Packages.props`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add security event log and database-backed Data Protection keys`.

### Task 5: Sign-in, roles from the database, bootstrap-admin

**Files:**
- Create in `src/CslaStarter.Infrastructure/Identity/`:
  - `AuthenticationSetup.cs`, `EntraClaims.cs`
  - `UserDirectory.cs`, `RoleClaimsTransformation.cs`
  - `UserRevalidatingAuthenticationStateProvider.cs`
  - `AdminBootstrapper.cs`, `SystemDataAccessContext.cs`
- Modify: `src/CslaStarter.Web/Program.cs` (the `bootstrap-admin` command, auth, fallback policy)
- Test: `tests/CslaStarter.Integration.Tests/IdentityTests.cs`, `BootstrapAdminTests.cs`, `WebAppFactory.cs`, `TestAuthHandler.cs`

**Interfaces (Produces):**

```csharp
public static IServiceCollection AddCslaStarterAuthentication(this IServiceCollection s, IConfiguration c, IHostEnvironment env); // Mode: Entra | MockOidc
public static class EntraClaims { string? TenantId(ClaimsPrincipal p); string? ObjectId(ClaimsPrincipal p); const string UserIdClaim = "csla_starter:user_id"; }
public interface IUserDirectory { Task<DirectoryUser> GetOrProvisionAsync(string tenantId, string objectId, string displayName, string? email, CancellationToken ct); void Invalidate(string tenantId, string objectId); }
public sealed record DirectoryUser(Guid Id, bool IsActive, IReadOnlySet<int> RoleCodes, string PreferredLanguage);
public sealed class AdminBootstrapper { Task<int> RunAsync(string tenantId, string objectId, string displayName, CancellationToken ct); } // 0 = done, 2 = refused
```

- **Cache:** `UserDirectory` caches for 5 minutes in `IMemoryCache`, keyed `(tid, oid)`. It creates a missing user with no roles and writes a `SignIn` security event the first time.
- **Claims transformation:**
  - It does nothing if `UserIdClaim` is already present.
  - It adds a `ClaimsIdentity("CslaStarter", ClaimTypes.Name, ClaimTypes.Role)` with `UserIdClaim`, one `ClaimTypes.Role` claim per role code (as a string), and `"csla_starter:lang"`.
  - An inactive user gets `UserIdClaim` only.
- **Revalidation:** `UserRevalidatingAuthenticationStateProvider` revalidates every 10 minutes and returns false when the user is inactive.
- **Command line:** `CslaStarter.Web bootstrap-admin --tenant <tid> --object-id <oid> [--name <name>]` uses `ConnectionStrings:Owner` and `SystemDataAccessContext("bootstrap-admin")`, then exits with `AdminBootstrapper`'s code.

- [ ] **Step 1: Write the failing tests.**
  - `ClaimsTransformation_FirstSignIn_CreatesUserWithNoRoles`
  - `ClaimsTransformation_AddsRoleClaimsFromDatabase`: the principal `IsInRole("1")` after role 1 is assigned.
  - `ClaimsTransformation_IsIdempotent`: transforming twice gives the same claim count.
  - `ClaimsTransformation_InactiveUser_GetsNoRoles`
  - `BootstrapAdmin_CreatesFirstAdmin_AndLogsSecurityEvent`: exit 0, the user has role 1, and an `AdminBootstrapped` row exists.
  - `BootstrapAdmin_RefusesWhenAnAdminExists`: the second run exits 2, and the row counts are unchanged.
  - `MockOidc_InProduction_RefusesToStart`: building the `WebAppFactory` with environment Production and `Authentication:Mode=MockOidc` throws `InvalidOperationException`.
  - `AuthCookie_HasRequiredSettings`: `CookieAuthenticationOptions` has Name `__Host-CslaStarter`, `SecurePolicy.Always`, `HttpOnly`, `SameSiteMode.Lax`, 8 hours, no sliding.
  - `Anonymous_RequestToHome_IsChallenged`: 302 or 401 with the test scheme.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Entra mode uses `AddMicrosoftIdentityWebApp(c.GetSection("AzureAd"))` with the cookie settings.
  - MockOidc mode uses Cookie + `AddOpenIdConnect` (Authority `Authentication:MockOidc:Authority`, `RequireHttpsMetadata=false`, `MapInboundClaims=false`, `ResponseType="code"`, `UsePkce`).
  - `WebAppFactory` swaps in `TestAuthHandler` and a per-test database.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add Entra and mock OIDC sign-in, database roles and bootstrap-admin`.

### Task 6: Web host hardening, localization, idle timeout, health and telemetry

**Files:**
- Create in `src/CslaStarter.Infrastructure/`:
  - `Web/SecurityHeadersMiddleware.cs`, `Web/CspNonce.cs`
  - `Localization/LocalizationSetup.cs`
  - `Sessions/SessionActivity.cs`, `Sessions/SessionActivityCircuitHandler.cs`, `Sessions/SessionTimeoutMonitor.cs`, `Sessions/SessionOptions.cs`
  - `Health/MigrationsHealthCheck.cs`, `Telemetry/TelemetrySetup.cs`
- Modify: `src/CslaStarter.Web/Program.cs`, `Components/App.razor` (csp-nonce meta, nonce on inline scripts, `lang`)
- Create: `docker-compose.yml`, `deploy/mock-oidc/config.json`, `src/CslaStarter.Web/appsettings.Development.json` (no secrets)
- Test: `tests/CslaStarter.Integration.Tests/WebHostTests.cs`, `SessionTimeoutTests.cs`

**Interfaces (Produces):**
- `CspNonce` is a scoped service with `string Value`.
- `SessionTimeoutMonitor` (scoped):
  - `SessionState State`, where `SessionState` is `Active`, `Warning` or `Expired`
  - `TimeSpan Remaining`
  - `event Action<SessionState>? StateChanged`
  - `void RecordActivity()`
  - it uses `TimeProvider` and `IOptions<SessionOptions>`.
- `MapCultureEndpoint()`: maps `/culture/set`. `MapCslaStarterHealth()`: maps `/health/live` (no checks) and `/health/ready` (Npgsql plus pending migrations).

- [ ] **Step 1: Write the failing tests.**
  - `SecurityHeaders_ArePresentWithFreshNonce`: two requests carry different nonces, and the CSP equals the Global Constraints string with that nonce.
  - `Csp_EveryInlineScriptCarriesTheNonce`: on the `/` HTML, every `<script>` without `src` has `nonce="<header nonce>"`, and a `<meta name="csp-nonce">` holds the same value.
  - `CultureSet_StoresCookieAndRedirectsLocally`
  - `CultureSet_RejectsOffSiteRedirect`: for `https://evil.example` and `//evil.example`, the response is not a 3xx to another host.
  - `CultureSet_IgnoresUnsupportedCulture`: `de-DE` sets no cookie.
  - `Html_LangFollowsCulture`: with the fr-CA cookie, `<html lang="fr-CA">`.
  - `HealthLive_IsAnonymousAndHealthy` and `HealthReady_ReportsHealthyDatabase`
  - `SessionTimeout_WarnsAt18Minutes`, `SessionTimeout_ExpiresAt20Minutes`, `SessionTimeout_ActivityResetsTheClock` (using `FakeTimeProvider`)
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - The nonce is 16 random bytes, base64.
  - If `<ImportMap>` can't carry the nonce, render the import map through a nonce'd script, or remove it if nothing uses it. Record a ruling either way.
  - `AddInteractiveServerComponents(o => o.DetailedErrors = env.IsDevelopment())`.
  - OTLP export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
  - `docker-compose.yml`: `postgres:18` with `create-roles.sql` mounted into `/docker-entrypoint-initdb.d`, and `ghcr.io/navikt/mock-oauth2-server:6.1.5` on port 8080 with `JSON_CONFIG_PATH`. The issuer `entra` maps claims `tid`, `oid`, `name` and `preferred_username` from the login subject.
- [ ] **Step 4: Run the tests.** Expected: PASS. Then run `dotnet format --verify-no-changes` and `bash scripts/tests/run.sh`.
- [ ] **Step 5: Commit** with the message `DEV-38 Add security headers, culture switching, idle timeout, health and telemetry`.

### Task 7: Documentation

**Files:** `docs/claude/security.md`, `docs/claude/data-access.md`, `docs/claude/localization.md` (new); `docs/claude/testing.md`, root `CLAUDE.md` (commands: `docker compose up -d`, `dotnet ef`, `bootstrap-admin`; guides list), `src/CslaStarter.{Core,Data,Infrastructure,Web}/CLAUDE.md`, `tests/CslaStarter.{Business,Integration}.Tests/CLAUDE.md`.

- [ ] **Step 1: Write the three guides** from spec §7–§10 and what Tasks 1–6 built (exact names, settings and commands).
- [ ] **Step 2: Update the CLAUDE.md files and the testing table.**
- [ ] **Step 3: Run `dotnet test`.** Expected: all pass, including `ClaudeDocsTests` (new test projects have a `CLAUDE.md`, and every link resolves).
- [ ] **Step 4: Commit** with the message `DEV-38 Document data access, security and localization`.
