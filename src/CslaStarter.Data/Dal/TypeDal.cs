using System.Collections.Concurrent;
using CslaStarter.Dal.Types;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Dal;

/// <summary>
/// Reads the *_type lookup tables by business type name. Lookups change only through migrations (a new deployment),
/// so each table is read once per process; the rows are immutable records, safe to share across users.
/// </summary>
public sealed class TypeDal(AppDbContext db) : ITypeDal
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<TypeDto>> Cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<TypeDto>> FetchAsync(string typeName, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        IReadOnlyList<TypeDto> rows = typeName switch
        {
            "UserRoleType" => await db.UserRoleTypes.AsNoTracking()
                .Select(t => new TypeDto(t.Code, t.DescriptionEn, t.DescriptionFr, t.IsActive, t.SortOrder))
                .ToListAsync(cancellationToken),
            "ProjectStatusType" => await db.ProjectStatusTypes.AsNoTracking()
                .Select(t => new TypeDto(t.Code, t.DescriptionEn, t.DescriptionFr, t.IsActive, t.SortOrder))
                .ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unknown *Type lookup."),
        };
        return Cache.GetOrAdd(typeName, rows);
    }

    /// <summary>For tests that change lookup rows.</summary>
    public static void ResetCache() => Cache.Clear();
}
