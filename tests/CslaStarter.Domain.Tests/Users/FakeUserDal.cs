using CslaStarter.Domain.Platform;
using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Users;

/// <summary>
/// Two users, Ada then Bea, in memory. Records every staged update; a staged row's version is the incoming
/// version + 1, readable only once <paramref name="work"/> has committed.
/// </summary>
internal sealed class FakeUserDal(CountingUnitOfWork work) : IUserDal
{
    public static readonly UserDto Ada = new(Guid.Parse("0193a5c0-0000-7000-8000-00000000000a"), "Ada", 7);
    public static readonly UserDto Bea = new(Guid.Parse("0193a5c0-0000-7000-8000-00000000000b"), "Bea", 3);

    private static readonly UserDto[] Users = [Ada, Bea];

    private readonly List<UserDto> _staged = [];

    public IReadOnlyList<UserDto> Staged => _staged;

    public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDto>>(Users);

    public Task<UserDto> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Id == id) ?? throw new NotFoundException("User", id));

    public IStagedRow StageUpdate(UserDto user)
    {
        _staged.Add(user);
        return new StagedRow(work, user.Version + 1);
    }

    private sealed class StagedRow(CountingUnitOfWork work, uint version) : IStagedRow
    {
        public uint Version => work.Commits > 0
            ? version
            : throw new InvalidOperationException("A staged row has no version until the unit of work commits.");
    }
}
