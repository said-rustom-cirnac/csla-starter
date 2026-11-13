using System.Globalization;
using Csla;
using CslaStarter.Core.Auditing;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Auditing;

/// <summary>
/// One changed field: its label (resource key <c>{EntityType}_{Property}</c>), and the values before and after. Codes of
/// a *Type show their description in the current language; masked (sensitive) values are never available.
/// </summary>
[Serializable]
public sealed class AuditHistoryChange : ReadOnlyBase<AuditHistoryChange>
{
    public static readonly PropertyInfo<string> FieldProperty = RegisterProperty<string>(nameof(Field));
    public static readonly PropertyInfo<string> FieldNameProperty = RegisterProperty<string>(nameof(FieldName));
    public static readonly PropertyInfo<string?> OldValueProperty = RegisterProperty<string?>(nameof(OldValue));
    public static readonly PropertyInfo<string?> NewValueProperty = RegisterProperty<string?>(nameof(NewValue));
    public static readonly PropertyInfo<bool> IsMaskedProperty = RegisterProperty<bool>(nameof(IsMasked));

    /// <summary>The property name, as the business object names it.</summary>
    [LocalizedDisplayName(nameof(Labels.History_Field))]
    public string Field => GetProperty(FieldProperty)!;

    /// <summary>The field's label in the current language.</summary>
    [LocalizedDisplayName(nameof(Labels.History_Field))]
    public string FieldName => GetProperty(FieldNameProperty)!;

    [LocalizedDisplayName(nameof(Labels.History_OldValue))]
    public string? OldValue => GetProperty(OldValueProperty);

    [LocalizedDisplayName(nameof(Labels.History_NewValue))]
    public string? NewValue => GetProperty(NewValueProperty);

    [LocalizedDisplayName(nameof(Labels.History_IsMasked))]
    public bool IsMasked => GetProperty(IsMaskedProperty);

    [FetchChild]
    private void FetchChild(string entityType, AuditChange change, IReadOnlyDictionary<string, IReadOnlyList<TypeDto>> typeRows)
    {
        LoadProperty(FieldProperty, change.Property);
        LoadProperty(FieldNameProperty, Labels.ResourceManager.GetString($"{entityType}_{change.Property}", CultureInfo.CurrentUICulture)
            ?? Labels.ResourceManager.GetString($"Common_{change.Property}", CultureInfo.CurrentUICulture)
            ?? change.Property);
        LoadProperty(OldValueProperty, Describe(change.OldValue, change.TypeCodeOf, typeRows));
        LoadProperty(NewValueProperty, Describe(change.NewValue, change.TypeCodeOf, typeRows));
        LoadProperty(IsMaskedProperty, change.IsMasked);
    }

    private static string? Describe(string? value, string? typeName, IReadOnlyDictionary<string, IReadOnlyList<TypeDto>> typeRows)
    {
        if (typeName is null || value is null || !typeRows.TryGetValue(typeName, out var rows)
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) || rows.FirstOrDefault(r => r.Code == code) is not { } row)
        {
            return value;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? row.DescriptionFr : row.DescriptionEn;
    }
}
