using CslaStarter.Core.Security;

namespace CslaStarter.Integration.Tests;

public sealed record FakeDataAccessContext(Guid? UserId, string UserName, string? CorrelationId = null, string? OriginId = null) : IDataAccessContext
{
    public static FakeDataAccessContext Tester { get; } = new(Guid.Parse("0193a000-0000-7000-8000-000000000001"), "tester", "corr-1");
}
