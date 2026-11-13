# CslaStarter.Core

Contracts and localization shared by every layer. This project has no infrastructure dependencies.

**May reference:** `Microsoft.Extensions.*.Abstractions` packages only. No project references.

## Holds

- `Security/`:
  - `IDataAccessContext`: who is making a data change.
  - `ISecurityEventLog`, `SecurityEvent`, `SecurityEventKind`.
  - `SystemRoles.Admin = 1`: the role code the platform depends on. The business `UserRoleType.Admin` uses it.
- `Auditing/`: `IAuditHistoryReader`, `AuditQuery`, `AuditEntry`, `AuditChange`, `AuditPage`.
- `Localization/`: `SupportedCultures` (`en-CA`, `fr-CA`) and `LocalizedDisplayNameAttribute`.
- `Realtime/`: `EntityChange`, `ChangeTopic`, `IEntityChangeHub`, `IEntityChangeNotifier`, `IPresenceTracker`, `PresenceEntry`.
- `Drafts/`: `IDraftStore`, `DraftKey`, `Draft`.
- `Resources/`: `Labels` and `Messages` resources (`.resx` + `.fr.resx`), as strongly-typed classes generated at build.

## Rules

- Never add EF Core, Npgsql, ASP.NET Core or CSLA here. `Core_ReferencesOnlyExtensionAbstractions` checks package references.
- Keep contracts free of persistence types. Use plain records and primitives.
- Every resource key needs a non-empty value in both languages ([localization](../../docs/claude/localization.md)).

Guides: [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md), [localization](../../docs/claude/localization.md), [realtime](../../docs/claude/realtime.md).
