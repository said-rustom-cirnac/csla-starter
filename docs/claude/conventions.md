# Conventions

## Naming

These are enforced by `.editorconfig` and `dotnet format --verify-no-changes`, which CI runs:

- File-scoped namespaces, with `using` directives outside the namespace.
- Constants and `static readonly` fields are PascalCase. Private instance fields are `_camelCase`.
- Test methods are `Method_Condition_Result`; underscores are allowed in `tests/` only.
- Don't name a type after a language keyword, such as `Error`. CA1716 fails the build.

## CSLA business classes

Members appear in this order:

1. **Property registrations:** `public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));`
2. **Properties:** `[LocalizedDisplayName(nameof(Labels.Project_Name))]` above `GetProperty`/`SetProperty`, or `LoadProperty` for private setters. Non-nullable reference types need `!` on `GetProperty`.
3. **Business rules:** `AddBusinessRules()`, then `[ObjectAuthorizationRules] AddObjectAuthorizationRules(...)`.
4. **Data portal operations:** `[Create]`, `[Fetch]`, `[Insert]`, `[Update]`, `[DeleteSelf]`, `[Delete]`, `[Execute]`, and the child variants.

`*Type` lookup classes put their `public const int` codes above step 1:

```csharp
public sealed class UserRoleType : CslaType<UserRoleType>
{
    public const int Admin = 1;
    public const int Client = 2;
    public const int Technical = 3;
}
```

Other CSLA rules:

- Don't use the `Csla.Generator.AutoImplementProperties` generator; it hides the registrations.
- Don't set properties in constructors. Use `[Create]`.

## Razor components

- `X.razor` holds markup only. `X.razor.cs` holds a `partial class X` with all code.
- `@code` and `@functions` blocks fail `RazorFiles_HaveNoCodeBlocks`.
- Every `.razor` file in `CslaStarter.Components` needs its `.razor.cs`, or `ComponentRazorFiles_HaveCodeBehind` fails.

## Nullable and analyzers

- Nullable reference types are on everywhere. `AnalysisLevel` is latest-recommended.
- Every warning is an error, including NuGet audit warnings for vulnerable packages.
- Package versions live only in `Directory.Packages.props`. A csproj `PackageReference` has no `Version`.
- Microsoft.* packages stay on 10.0.x. Never take 11.x.
