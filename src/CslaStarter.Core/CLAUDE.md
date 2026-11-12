# CslaStarter.Core

Contracts and localization shared by every layer. This project has no infrastructure dependencies.

**May reference:** `Microsoft.Extensions.*.Abstractions` packages only. No project references.

## Holds

- **Interfaces other layers implement or consume:** `ICurrentUser`, `IEntityChangeNotifier` / `IEntityChangeHub`, `IPresenceTracker`, `IDraftStore`, `IAuditHistoryReader`, `ISecurityEventLog`, `IClock`.
- **Resources:** `Labels.resx` + `Labels.fr.resx` hold field names; `Messages.resx` + `Messages.fr.resx` hold rule, error and UI text. Every key exists, non-empty, in both languages.
- **Attributes:** `LocalizedDisplayNameAttribute`, a subclass of `DisplayNameAttribute` that reads `Labels` for the current UI culture, and `SensitiveAttribute`.

## Rules

- Never add EF Core, Npgsql, ASP.NET Core or CSLA here. `Core_ReferencesOnlyExtensionAbstractions` fails if you do.
- Keep contracts free of persistence types. Use plain records and primitives.

Guides: [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
