namespace CslaStarter.Domain.Platform.Users;

/// <summary>Reads users and stages changes to them. There is no insert or delete: accounts come from sign-in.</summary>
public interface IUserDal
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);

    /// <summary>The user with <paramref name="id"/>; throws <see cref="NotFoundException"/> when there is none.</summary>
    Task<UserDto> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Stages the change; it is written when the unit of work commits.</summary>
    IStagedRow StageUpdate(UserDto user);
}
