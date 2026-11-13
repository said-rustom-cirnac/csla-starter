---
name: new-type
description: Use when adding a *Type lookup (a code list such as a status or category) with English and French descriptions and code constants. Follows docs/claude/recipes/add-a-type.md.
---

# New `*Type`

Follow [add a type](../../../docs/claude/recipes/add-a-type.md):

1. **Data:** the row entity (`Code`, `DescriptionEn`, `DescriptionFr`, `IsActive`, `SortOrder`) and its configuration, seeded with `HasData` in a migration.
2. **Business:** `<Name>Type : CslaType<<Name>Type>` with `public const int` codes matching the seeded rows, and `<Name>TypeList : CslaTypeList<…>`. Add a case for the type in `TypeDal`.
3. **Use it:**
   - Store codes on entities with `[AuditTypeCode("<Name>Type")]`, so the history shows descriptions.
   - Offer it in forms with `TypeOptions.From(list, current)`: active values plus the record's current one.
   - Retire a value by setting `IsActive = false`; never delete it.
4. **Tests:** `TypeSeedConsistencyTests` checks the constants against the seed. Add the type there.
5. **Run** `dotnet test`.
