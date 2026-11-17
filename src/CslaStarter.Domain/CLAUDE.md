# CslaStarter.Domain

The CSLA business objects with their rules and permissions, the DAL contracts and DTOs they call, and the translator. Today it holds only the `DomainAssembly` marker and the empty `Platform/` and `Features/` folders.

- References no other project, and no EF Core, Npgsql or ASP.NET Core assembly.
- `Platform/` is starter-owned and never references `Features/`.
- Tests: `tests/CslaStarter.Architecture.Tests` checks the references and boundaries. `CslaStarter.Domain.Tests` is created with the first code it tests.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
