using CslaStarter.Dal;
using CslaStarter.Dal.Users;

namespace CslaStarter.Business.Tests.Users;

public sealed class FakeUserDal : IUserDal
{
    public Dictionary<Guid, UserDto> Users { get; } = [];

    public List<Guid> Fetched { get; } = [];

    public UserDto Add(Guid id, string name, params int[] roles)
    {
        var user = new UserDto(id, "tenant-1", $"oid-{id}", name, $"{name}@example.ca", "en-CA", true, "E-1", roles, 7,
            DateTimeOffset.UnixEpoch, "seed", DateTimeOffset.UnixEpoch, "seed");
        Users[id] = user;
        return user;
    }

    public Task<UserDto> FetchAsync(Guid id, CancellationToken cancellationToken)
    {
        Fetched.Add(id);
        return Users.TryGetValue(id, out var user) ? Task.FromResult(user) : throw new NotFoundException("User", id.ToString());
    }

    public Task<PagedResult<UserInfoDto>> FetchListAsync(UserListCriteria criteria, CancellationToken cancellationToken)
    {
        var items = Users.Values.Select(u => new UserInfoDto(u.Id, u.DisplayName, u.Email, u.IsActive, u.RoleCodes)).ToList();
        return Task.FromResult(new PagedResult<UserInfoDto>(items, items.Count));
    }

    public Task<UserDto> UpdateAsync(UserUpdateDto user, CancellationToken cancellationToken)
    {
        var current = Users[user.Id];
        if (current.Version != user.Version)
        {
            throw new ConcurrencyException("User", user.Id.ToString());
        }

        Users[user.Id] = current with { DisplayName = user.DisplayName, IsActive = user.IsActive, EmployeeNumber = user.EmployeeNumber, RoleCodes = user.RoleCodes, Version = current.Version + 1 };
        return Task.FromResult(Users[user.Id]);
    }

    public Task<UserDto> UpdateProfileAsync(UserProfileUpdateDto profile, CancellationToken cancellationToken)
    {
        var current = Users[profile.Id];
        Users[profile.Id] = current with { DisplayName = profile.DisplayName, PreferredLanguage = profile.PreferredLanguage, Version = current.Version + 1 };
        return Task.FromResult(Users[profile.Id]);
    }
}
