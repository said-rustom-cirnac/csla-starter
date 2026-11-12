using System.Reflection;
using CslaStarter.Business.Types;
using CslaStarter.Data.Dal;

namespace CslaStarter.Integration.Tests;

/// <summary>Every public const code on a *Type class has a seeded row, and every seeded row has a const.</summary>
public sealed class TypeSeedConsistencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public static TheoryData<string> TypeNames() =>
        [.. typeof(UserRoleType).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() == typeof(CslaType<>))
            .Select(t => t.Name)];

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        TypeDal.ResetCache();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [MemberData(nameof(TypeNames))]
    public async Task TypeConstants_MatchSeededRows(string typeName)
    {
        var type = typeof(UserRoleType).Assembly.GetTypes().Single(t => t.Name == typeName);
        var constants = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .Select(f => (int)f.GetRawConstantValue()!)
            .Order();
        await using var context = _db.CreateContext();

        var rows = await new TypeDal(context).FetchAsync(typeName, TestContext.Current.CancellationToken);

        Assert.Equal(constants, rows.Select(r => r.Code).Order());
    }
}
