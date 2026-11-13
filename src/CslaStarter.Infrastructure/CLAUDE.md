# CslaStarter.Infrastructure

Runtime services that aren't data access. Each folder exposes one `AddCslaStarter…` / `Use…` / `Map…` extension:

| Folder | What |
|---|---|
| `Identity/` | Entra and mock OIDC sign-in, the `IUserDirectory` cache, `RoleClaimsTransformation`, revalidation, `AdminBootstrapper` and the `bootstrap-admin` command, the data access contexts |
| `DataProtection/` | Keys in PostgreSQL, protected by a certificate or Key Vault |
| `Web/` | `CspNonce` and the security headers middleware |
| `Localization/` | Request culture providers and `/culture/set` |
| `Sessions/` | `SessionTimeoutMonitor` (activity is reported by the session-timeout component) |
| `Health/` | `/health/live` and `/health/ready` (database, pending migrations) |
| `Telemetry/` | OpenTelemetry tracing, metrics and logs (OTLP when configured) |
| `Realtime/` | `EntityChangeHub`, `PostgresChangeBackplane` (LISTEN/NOTIFY), `PresenceTracker`, `DraftCleanupService` |

**May reference:** Core, Data, Npgsql, and the shared ASP.NET Core framework.

## Rules

- **Claims transformation** must stay idempotent; it may run several times per request.
- **Lifetimes:** singletons never take scoped services. Integration tests validate scopes like Development does.
- **Change notices** carry type, key, version and who made the change, never field data. Notices are hints; optimistic concurrency is the guarantee.
- **Hub handlers** run concurrently and must return quickly. `UserDirectory` and the revalidation provider subscribe to access-changed notices.

Guides: [security](../../docs/claude/security.md), [localization](../../docs/claude/localization.md), [architecture](../../docs/claude/architecture.md), [realtime](../../docs/claude/realtime.md).
