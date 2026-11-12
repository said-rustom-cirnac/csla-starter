using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Dal;

internal static class DalHelpers
{
    /// <summary>
    /// Loads the aggregate root's version check: the UPDATE of the root carries <c>WHERE xmin = version</c>, so the save
    /// fails if anyone changed any part of the aggregate since the user loaded it.
    /// </summary>
    public static void ExpectVersion(DbContext context, IAggregateRoot root, uint version) =>
        context.Entry(root).Property(nameof(IAggregateRoot.Version)).OriginalValue = version;

    /// <summary>
    /// Makes sure the root row is updated (bumping its xmin) even when only its children changed, so concurrent edits
    /// to any part of the aggregate collide on the root's version.
    /// </summary>
    public static void TouchRoot(DbContext context, IAggregateRoot root)
    {
        var entry = context.Entry(root);
        if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }
    }

    public static async Task SaveAggregateAsync(DbContext context, string entityType, Guid key, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new CslaStarter.Dal.ConcurrencyException($"{entityType} {key} was changed by someone else.", e);
        }
    }

    /// <summary>Escapes LIKE wildcards so user search text is matched literally.</summary>
    public static string ContainsPattern(string text) =>
        "%" + text.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";
}
