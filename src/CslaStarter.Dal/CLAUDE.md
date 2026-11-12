# CslaStarter.Dal

The data access contract: one DAL interface per business object, the DTOs it exchanges, and the DAL exceptions.

**May reference:** Core.

## Rules

- **Interfaces:** named `I<Thing>Dal`, all async, and every method takes a `CancellationToken`.
- **DTOs:** plain records. Aggregate roots carry `Version` (`uint`, PostgreSQL `xmin`).
- **Exceptions:** a stale version throws `ConcurrencyException`; a missing or hidden record throws `NotFoundException`.
- **Callers:** business objects receive DALs as `[Inject]` parameters on their data portal methods. Nothing else calls a DAL.
- **No EF Core:** EF types never appear here. The implementations live in `CslaStarter.Data`.

Guides: [architecture](../../docs/claude/architecture.md).
