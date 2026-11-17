# CslaStarter.Data

EF Core and PostgreSQL: the unit of work, audit capture and the implementations of the DAL contracts in `Domain`. Today it holds only the `DataAssembly` marker and the empty `Platform/` and `Features/` folders.

- References `CslaStarter.Domain` only. The compiled assembly never references `Csla`: it sees only DAL contracts and DTOs.
- Only `Platform/` code calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL. A feature never uses another feature's entities or DAL.
- Tests: `tests/CslaStarter.Architecture.Tests` checks the references and boundaries. Tests against a real PostgreSQL arrive with the first DAL.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
