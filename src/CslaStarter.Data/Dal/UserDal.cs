using CslaStarter.Core.Security;
using CslaStarter.Dal;
using CslaStarter.Dal.Users;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Dal;

public sealed class UserDal(AppDbContext db, IUserRoleCacheInvalidator invalidator, ISecurityEventLog securityEvents, IDataAccessContext access) : IUserDal
{
    public async Task<UserDto> FetchAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id.ToString());
        return Map(user);
    }

    public async Task<PagedResult<UserInfoDto>> FetchListAsync(UserListCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var pattern = DalHelpers.ContainsPattern(criteria.Search.Trim());
            users = users.Where(u => EF.Functions.ILike(u.DisplayName, pattern) || (u.Email != null && EF.Functions.ILike(u.Email, pattern)));
        }

        if (criteria.RoleCode is { } role)
        {
            users = users.Where(u => u.Roles.Any(r => r.RoleTypeCode == role));
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Skip((Math.Max(criteria.Page, 1) - 1) * criteria.PageSize).Take(Math.Clamp(criteria.PageSize, 1, 200))
            .Select(u => new UserInfoDto(u.Id, u.DisplayName, u.Email, u.IsActive, u.Roles.Select(r => r.RoleTypeCode).OrderBy(c => c).ToList()))
            .ToListAsync(cancellationToken);
        return new PagedResult<UserInfoDto>(items, total);
    }

    public async Task<UserDto> UpdateAsync(UserUpdateDto user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var row = await LoadForUpdateAsync(user.Id, user.Version, cancellationToken);
        var rolesBefore = RoleList(row.Roles.Select(r => r.RoleTypeCode));
        row.DisplayName = user.DisplayName;
        row.IsActive = user.IsActive;
        row.EmployeeNumber = user.EmployeeNumber;

        foreach (var removed in row.Roles.Where(r => !user.RoleCodes.Contains(r.RoleTypeCode)).ToList())
        {
            row.Roles.Remove(removed);
        }

        foreach (var added in user.RoleCodes.Except(row.Roles.Select(r => r.RoleTypeCode)).ToList())
        {
            row.Roles.Add(new UserRole { RoleTypeCode = added });
        }

        DalHelpers.TouchRoot(db, row);
        await DalHelpers.SaveAggregateAsync(db, "User", user.Id, cancellationToken);
        invalidator.Invalidate(row.TenantId, row.ObjectId);

        var rolesAfter = RoleList(row.Roles.Select(r => r.RoleTypeCode));
        if (rolesAfter != rolesBefore)
        {
            await securityEvents.WriteAsync(
                new SecurityEvent(SecurityEventKind.RoleChanged, access.UserId, access.UserName, $"User {user.Id} roles {rolesBefore} -> {rolesAfter}", access.CorrelationId),
                cancellationToken);
        }

        return await FetchAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateProfileAsync(UserProfileUpdateDto profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var row = await LoadForUpdateAsync(profile.Id, profile.Version, cancellationToken);
        row.DisplayName = profile.DisplayName;
        row.PreferredLanguage = profile.PreferredLanguage;
        DalHelpers.TouchRoot(db, row);
        await DalHelpers.SaveAggregateAsync(db, "User", profile.Id, cancellationToken);
        invalidator.Invalidate(row.TenantId, row.ObjectId);
        return await FetchAsync(profile.Id, cancellationToken);
    }

    private static string RoleList(IEnumerable<int> codes) => string.Join(',', codes.Order());

    private async Task<AppUser> LoadForUpdateAsync(Guid id, uint version, CancellationToken cancellationToken)
    {
        var row = await db.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id.ToString());
        DalHelpers.ExpectVersion(db, row, version);
        return row;
    }

    private static UserDto Map(AppUser user) => new(
        user.Id, user.TenantId, user.ObjectId, user.DisplayName, user.Email, user.PreferredLanguage, user.IsActive, user.EmployeeNumber,
        user.Roles.Select(r => r.RoleTypeCode).Order().ToList(), user.Version, user.CreatedOn, user.CreatedBy, user.ModifiedOn, user.ModifiedBy);
}
