# CslaStarter.Domain

The CSLA business objects with their rules and permissions, the DAL contracts and DTOs they call, and the translator.

What it holds today:

- `Platform/Texts/`: the text entry point `Text` (`Text.T("…")`, `Text.Error(ErrorMessageType.…)`), the `ITranslator` contract and its default `SourceTextTranslator`, the seed-file fallback for error messages, the `ErrorMessageType` codes, and `[Label("…")]`.
- `Platform/Rules/`: the rule attributes `[RequiredField]` and `[MaxLengthOf(n)]`. Each one names its message by an `ErrorMessageType` constant. New rule attributes and rule classes go here.
- `Features/` is still empty.

Rules:

- References no other project, and no EF Core, Npgsql or ASP.NET Core assembly.
- `Platform/` is starter-owned and never references `Features/`.
- Tests: `tests/CslaStarter.Domain.Tests` (see its `CLAUDE.md`). `tests/CslaStarter.Architecture.Tests` checks the references and boundaries.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
