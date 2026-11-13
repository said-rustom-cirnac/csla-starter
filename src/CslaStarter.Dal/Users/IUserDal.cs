namespace CslaStarter.Dal.Users;

public interface IUserDal
{
    /// <exception cref="NotFoundException">No such user.</exception>
    Task<UserDto> FetchAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<UserInfoDto>> FetchListAsync(UserListCriteria criteria, CancellationToken cancellationToken);

    /// <exception cref="ConcurrencyException">The user was changed since <see cref="UserUpdateDto.Version"/>.</exception>
    Task<UserDto> UpdateAsync(UserUpdateDto user, CancellationToken cancellationToken);

    /// <exception cref="ConcurrencyException">The user was changed since <see cref="UserProfileUpdateDto.Version"/>.</exception>
    Task<UserDto> UpdateProfileAsync(UserProfileUpdateDto profile, CancellationToken cancellationToken);
}

/// <summary>Forgets cached sign-in data for a user after their roles, status or language change.</summary>
public interface IUserRoleCacheInvalidator
{
    void Invalidate(string tenantId, string objectId);
}
