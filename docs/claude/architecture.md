# Architecture

## Projects

| Project | Holds | May reference |
|---|---|---|
| `CslaStarter.Core` | Contracts used across layers (`ICurrentUser`, `IEntityChangeHub`, `IPresenceTracker`, `IDraftStore`, `IAuditHistoryReader`, `ISecurityEventLog`, `IClock`), `.resx` resources, `LocalizedDisplayNameAttribute`, `SensitiveAttribute` | `Microsoft.Extensions.*.Abstractions` only |
| `CslaStarter.Dal` | DAL interfaces, DTOs, DAL exceptions (`ConcurrencyException`, `NotFoundException`) | Core |
| `CslaStarter.Business` | CSLA business objects, base classes, `CslaType<T>` lookups, rules and authorization rules | Core, Dal, Csla |
| `CslaStarter.Data` | `AppDbContext`, entities, migrations, DAL implementations, audit interceptor, draft store, security event log, Data Protection key store | Core, Dal, EF Core, Npgsql |
| `CslaStarter.Infrastructure` | Change hub and PostgreSQL backplane, presence, claims transformation, culture, session timeout, telemetry, health checks | Core, Data, Npgsql |
| `CslaStarter.Components` | The `CslaBaseComponent` family and GC Design System interop | Core, Business, Csla.Blazor |
| `CslaStarter.Web` | Host and composition root: `Program.cs`, sign-in, security headers, endpoints, pages | all |

Components never reference Data or Infrastructure. Business never references EF Core or Npgsql. `CslaStarter.Architecture.Tests` enforces both.

## How a request flows

1. The browser holds only rendered UI. Every event goes over the Blazor Server circuit (SignalR).
2. A component calls a CSLA business object through the data portal, which runs in-process.
3. The business object checks its rules and permissions, then calls a DAL interface it received through `[Inject]`.
4. The EF Core DAL writes to PostgreSQL. In the same transaction, the audit interceptor records old and new values.
5. After commit, the change is published to the change hub. Open screens showing that record, or a list of that type, refresh. With more than one server, the notice travels over PostgreSQL LISTEN/NOTIFY.

## Key decisions

- **UI host:** Blazor Server with global interactive server rendering and prerendering off. Business objects never reach the browser.
- **Data access:** DAL interfaces with an EF Core + Npgsql implementation. Tests swap in fakes.
- **Concurrency:** per aggregate, using the root row's PostgreSQL `xmin`. Every save touches the root.
- **Sign-in:** Entra ID. Roles come from our database, several per user, checked with `HasRole(UserRoleType.Admin)`.
- **Record-level access:** an EF Core named query filter, `"RecordAccess"`, hides records; CSLA object rules decide per-record edits. A hidden record reads as "not found".
- **Audit:** every change is captured with its old and new values in insert-only tables. The `CslaHistory` component shows them.
- **Live updates:** an in-memory hub on one server; a PostgreSQL backplane on several. Notices carry no field data.
- **UI kit:** GC Design System 1.7.0 web components, self-hosted, wrapped by the `Csla*` components.
- **Hosting:** on-premises IIS or Azure App Service, one instance or many.
