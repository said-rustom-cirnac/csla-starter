using System.Globalization;
using Csla;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Types;

/// <summary>
/// A small lookup persisted in its own *_type table (code, English and French descriptions, active flag, order).
/// Subclasses declare <c>public const int</c> codes so rules can say <c>HasRole(UserRoleType.Admin)</c>.
/// </summary>
[Serializable]
public abstract class CslaType<T> : ReadOnlyBase<T>
    where T : CslaType<T>
{
    public static readonly PropertyInfo<int> CodeProperty = RegisterProperty<int>(nameof(Code));
    public static readonly PropertyInfo<string> DescriptionEnProperty = RegisterProperty<string>(nameof(DescriptionEn));
    public static readonly PropertyInfo<string> DescriptionFrProperty = RegisterProperty<string>(nameof(DescriptionFr));
    public static readonly PropertyInfo<bool> IsActiveProperty = RegisterProperty<bool>(nameof(IsActive));
    public static readonly PropertyInfo<int> SortOrderProperty = RegisterProperty<int>(nameof(SortOrder));

    [LocalizedDisplayName(nameof(Labels.Type_Code))]
    public int Code => GetProperty(CodeProperty);

    [LocalizedDisplayName(nameof(Labels.Type_DescriptionEn))]
    public string DescriptionEn => GetProperty(DescriptionEnProperty)!;

    [LocalizedDisplayName(nameof(Labels.Type_DescriptionFr))]
    public string DescriptionFr => GetProperty(DescriptionFrProperty)!;

    [LocalizedDisplayName(nameof(Labels.Type_IsActive))]
    public bool IsActive => GetProperty(IsActiveProperty);

    [LocalizedDisplayName(nameof(Labels.Type_SortOrder))]
    public int SortOrder => GetProperty(SortOrderProperty);

    /// <summary>The description in the current UI language.</summary>
    public string Description =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? DescriptionFr : DescriptionEn;

    public override string ToString() => Description;

    [FetchChild]
    protected void FetchChild(TypeDto row)
    {
        ArgumentNullException.ThrowIfNull(row);
        LoadProperty(CodeProperty, row.Code);
        LoadProperty(DescriptionEnProperty, row.DescriptionEn);
        LoadProperty(DescriptionFrProperty, row.DescriptionFr);
        LoadProperty(IsActiveProperty, row.IsActive);
        LoadProperty(SortOrderProperty, row.SortOrder);
    }
}
