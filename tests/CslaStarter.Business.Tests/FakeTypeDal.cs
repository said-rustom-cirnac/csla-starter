using CslaStarter.Dal.Types;

namespace CslaStarter.Business.Tests;

public sealed class FakeTypeDal : ITypeDal
{
    public Dictionary<string, List<TypeDto>> Rows { get; } = new(StringComparer.Ordinal)
    {
        ["UserRoleType"] =
        [
            new(1, "Administrator", "Administrateur", true, 1),
            new(2, "Client", "Client", true, 2),
            new(3, "Technical", "Technique", true, 3),
            new(9, "Retired role", "Rôle retiré", false, 9),
        ],
    };

    public Task<IReadOnlyList<TypeDto>> FetchAsync(string typeName, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TypeDto>>(Rows[typeName]);
}
