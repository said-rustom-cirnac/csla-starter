# CslaStarter.Domain.Tests

Unit tests for `CslaStarter.Domain`. The folders mirror `src/CslaStarter.Domain/Platform/`: `Texts/` tests the text entry point, the default translator and `[Label]`; `Rules/` tests the rule attributes.

- References only `CslaStarter.Domain`; no database, no web host.
- Tests never call `Text.Use`: it is process-wide and tests run in parallel. They rely on the default `SourceTextTranslator`.
- `Culture.Use("fr-CA")` switches the current culture and UI culture for a test and restores both on dispose.
- Run: `dotnet test --project tests/CslaStarter.Domain.Tests` (one test: add `--filter-method "*Name"`).
