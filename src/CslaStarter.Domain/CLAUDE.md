# CslaStarter.Domain

The CSLA business objects with their rules and permissions, the DAL contracts and DTOs they call, and the translator.

## What it holds today

- `Platform/`: the base classes `AppRootBusinessBase<T>` (an aggregate root, with `Version` and `LoadVersion`), `AppReadOnlyBase<T>` and `AppReadOnlyListBase<TList, TItem>`; the contracts `IUnitOfWork`, `IStagedRow` and `ICurrentUser`; the DAL exceptions `NotFoundException` and `ConcurrencyException` (`DalExceptions.cs`).
- `Platform/Security/`: `AppClaimTypes` (the claims added at sign-in) and `CslaCurrentUser`, the `ICurrentUser` read from CSLA's `ApplicationContext.Principal`. The claims count only on an authenticated principal; a missing or malformed claim reads as `UserId == null` or `DisplayName == ""`, never as an exception.
- `Platform/Rules/`: the rule attributes `[RequiredField]` and `[MaxLengthOf(n)]`, and the authorization rule `IsSignedIn`. New rule attributes and rule classes go here.
- `Platform/Texts/`: the text entry point `Text` (`Text.T("…")`, `Text.Error(ErrorMessageType.…)`), the `ITranslator` contract and its default `SourceTextTranslator`, the seed-file fallback for error messages, the `ErrorMessageType` codes, and `[Label("…")]`.
- `Platform/Users/`: `UserEdit`, `UserList` with its rows `UserInfo`, the DAL contract `IUserDal` and `UserDto`. Any signed-in user may list users and fetch and edit one; roles arrive in slice 2.
- `Features/` is still empty.

## Patterns to copy

- **A business class:** `Platform/Users/UserEdit.cs`. `[CslaImplementProperties]` on a `sealed partial` class, `partial` properties carrying `[Label]` and the rule attributes, then `AddBusinessRules` (it calls `base.AddBusinessRules()`, which turns the attributes into rules), then the static `[ObjectAuthorizationRules]` method, then the data portal operations, each with its attribute (`[Fetch]`, `[Update]`). `Update` stages the row through the DAL, awaits `IUnitOfWork.CommitAsync()`, then takes the new version with `LoadVersion(staged.Version)`.
- **A read-only list:** `Platform/Users/UserList.cs` with its row `Platform/Users/UserInfo.cs`. The list fetches the DTOs and adds `IChildDataPortal<UserInfo>.FetchChild(dto)` inside `using (LoadListMode)`; the row loads itself in a `[FetchChild]` method.
- **A DAL contract:** `Platform/Users/IUserDal.cs` with `UserDto.cs`. Reads take a `CancellationToken`; a missing row throws `NotFoundException`. Writes only stage the change and return an `IStagedRow`.
- **A rule:** `Platform/Rules/`. A shape rule is an attribute (`RequiredFieldAttribute.cs`); logic or a permission is a rule class (`IsSignedIn.cs`). Each rule has its own `<RuleName>Tests` class in `tests/CslaStarter.Domain.Tests/Rules/` (`RequiredFieldAttributeTests`, `MaxLengthOfAttributeTests`, `IsSignedInTests`), with an outcome table at the top and one test or theory row per outcome.

## Pitfalls

- **Base class names.** CSLA 10.2's property generator recognises a class by its direct base class's name, which must contain `BusinessBase`, `ReadOnlyBase` or `CommandBase`. With any other name it emits an empty partial, and the build fails with `CS9248: Partial property '…' must have an implementation part`. That is why the root base is `AppRootBusinessBase<T>`; a child base will be `AppChildBusinessBase<T>`. `BaseClassNamingTests` fails if an abstract base class in this assembly is misnamed.
- **Do not hand-write properties in business classes.** For a non-nullable reference type the only getter the nullable checks accept is `GetProperty(XProperty)!`, and CSLA 10.2's analyzer reports CSLA0007 on it. Use the generator. The base class `AppRootBusinessBase<T>` hand-writes `Version` on purpose: it is a value type, so no `!` is needed, and it is read with `ReadProperty` so a read rule can never hide it from a save.
- **Refused access.** A refused fetch (`IDataPortal<T>.FetchAsync`) or save (`SaveAsync`) throws `Csla.Security.SecurityException` itself, not wrapped, before any operation runs, so nothing is staged. It derives from `Exception`, not from `System.Security.SecurityException`, so catch the CSLA type.
- **Not found.** A fetch for an id the DAL does not have throws `Csla.DataPortalException`. Its `InnerException` is a `Csla.Reflection.CallMethodException`, whose `InnerException` is the DAL's `NotFoundException`; `DataPortalException.BusinessException` returns that `NotFoundException`. Test `ex.BusinessException is NotFoundException`, not the outer type.
- **Saving an invalid object** throws `Csla.Rules.ValidationException` and stages nothing.
- **Saving returns a new object:** always `obj = await obj.SaveAsync()`.

## Rules

- References no other project, and no EF Core, Npgsql or ASP.NET Core assembly.
- `Platform/` is starter-owned and never references `Features/`.
- Tests: `tests/CslaStarter.Domain.Tests` (see its `CLAUDE.md`). `tests/CslaStarter.Architecture.Tests` checks the references and boundaries.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
