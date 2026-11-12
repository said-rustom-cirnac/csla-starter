# Recipe: add a `*Type` lookup

1. **Business:** add `Business/Types/XType.cs`:

   ```csharp
   [Serializable]
   public sealed class XType : CslaType<XType>
   {
       public const int First = 1;
       public const int Second = 2;
   }

   [Serializable]
   public sealed class XTypeList : CslaTypeList<XTypeList, XType>;
   ```

2. **Data:**
   - Add an `XTypeRow` entity (Code, DescriptionEn, DescriptionFr, IsActive, SortOrder).
   - Configure it with `ToTable("x_type")`, `ValueGeneratedNever()` on Code, and `HasData(...)` with one row per constant, in English and French.
   - Add the `DbSet` and a case in `TypeDal.FetchAsync` named exactly `"XType"`.
3. **Migration:** `dotnet ef migrations add AddXType ...`.
4. **Use it:** in a business object, reference `XType.First`. In a component, fetch `XTypeList` and offer `list.ActiveOrCurrent(currentCode)`, which keeps a retired value that is still in use.
5. **Run** `dotnet test`. `TypeConstants_MatchSeededRows` fails until the constants and the seed rows match exactly.

Codes are owned by code: change them only with a migration. Never reuse a retired code; mark it `IsActive = false`.
