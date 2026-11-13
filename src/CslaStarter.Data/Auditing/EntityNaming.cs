using System.Globalization;
using System.Reflection;
using CslaStarter.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CslaStarter.Data.Auditing;

/// <summary>How a row is named in the history and in change notices, so both always agree with the business objects.</summary>
internal static class EntityNaming
{
    private static readonly HashSet<Type> Platform =
        [typeof(AuditEventRow), typeof(AuditChangeRow), typeof(SecurityEventRow), typeof(DataProtectionKey)];

    /// <summary>Platform rows and <see cref="AuditIgnoreAttribute"/> entities: never audited, never announced.</summary>
    public static bool IsIgnored(Type type) =>
        Platform.Contains(type) || type.GetCustomAttribute<AuditIgnoreAttribute>() is not null;

    public static string TypeName(Type type) => type.GetCustomAttribute<AuditEntityAttribute>()?.Name ?? type.Name;

    public static string Key(EntityEntry entry) =>
        string.Join('|', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Format(entry.Property(p.Name).CurrentValue)));

    public static string? Format(object? value) => value switch
    {
        null => null,
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
