# CslaStarter.Domain.Tests

Unit tests for `CslaStarter.Domain`: business objects, rules, permissions and the data portal flow, against fake DALs. The folders mirror `src/CslaStarter.Domain/Platform/`: `Texts/`, `Rules/`, `Security/` and `Users/`. `BaseClassNamingTests` checks that every base class carries the name of its CSLA base (see the pitfalls in the Domain `CLAUDE.md`).

- References only `CslaStarter.Domain` and `Csla.Testing`; no database, no web host.
- **`TestPortal`** (`TestPortal.cs`) is CSLA configured as the host configures it (`UseLegacyOperationMethods = false`), built with `Csla.Testing`'s `CslaTestHost`, over `FakeUserDal` and a `CountingUnitOfWork`, and signed in as Ada with the `AppClaimTypes` claims. It exposes `FetchAsync<T>(criteria)`, `Dal`, `Work` and `SignedOut()`. Each test class creates its own portal in a field and disposes it, so tests share nothing. Copy `Users/UserEditTests.cs`.
- **Fake DALs** sit next to the tests that use them (`Users/FakeUserDal.cs`): fixed rows in a fixed order, and a record of every staged write. A staged row's version is readable only after `CountingUnitOfWork` has committed, as with the real unit of work.
- **Rules:** a rule is tested on its own with `Csla.Testing.Rules.AuthorizationRuleTester` (or `BusinessRuleTester`), for every outcome. Which business types use it is tested with `BusinessRules.HasPermission` on a `CslaTestHost` created `AsUser(...)` or `AsUnauthenticated()`. Copy `Rules/IsSignedInTests.cs`.
- Tests never call `Text.Use`: it is process-wide and tests run in parallel. They rely on the default `SourceTextTranslator`.
- `Culture.Use("fr-CA")` switches the current culture and UI culture for a test and restores both on dispose.
- Pass `TestContext.Current.CancellationToken` to any method that takes a token; the xUnit analyzer requires it.
- Run: `dotnet test --project tests/CslaStarter.Domain.Tests` (one test: add `--filter-method "*Name"`).
