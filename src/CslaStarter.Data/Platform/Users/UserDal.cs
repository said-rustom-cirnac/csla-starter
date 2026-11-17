using CslaStarter.Domain.Platform;
using CslaStarter.Domain.Platform.Users;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform.Users;

/// <summary>
/// Reads users. The reads use their own context for now; saving a user waits for the unit of work, which will
/// carry the reads and the staged update in one transaction.
/// </summary>
public sealed class UserDal(IDbContextFactory<AppDbContext> contexts) : IUserDal
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.DisplayName, u.Version))
            .ToListAsync(ct);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.DisplayName, u.Version))
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", id);
    }

    public IStagedRow StageUpdate(UserDto user) =>
        throw new NotImplementedException("Saving a user needs the unit of work, which is not built yet.");
}
