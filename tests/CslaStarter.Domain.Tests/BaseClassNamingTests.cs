using Csla;

namespace CslaStarter.Domain.Tests;

/// <summary>
/// CSLA 10.2's property generator recognises a class by its direct base class's name. A base class we write must
/// carry the name of the CSLA base it derives from, or the generator emits an empty partial and the build fails
/// with CS9248.
/// </summary>
public class BaseClassNamingTests
{
    private static readonly Type[] CslaBases = [typeof(BusinessBase<>), typeof(ReadOnlyBase<>), typeof(CommandBase<>)];

    [Fact]
    public void AbstractBases_CarryTheNameOfTheirCslaBase()
    {
        var misnamed = typeof(DomainAssembly).Assembly.GetTypes()
            .Where(type => type.IsClass && type.IsAbstract)
            .SelectMany(type => CslaBases
                .Where(cslaBase => DerivesFrom(type, cslaBase))
                .Select(NameOf)
                .Where(name => !type.Name.Contains(name, StringComparison.Ordinal))
                .Select(name => $"{type.FullName} must have \"{name}\" in its name"))
            .ToList();

        Assert.Empty(misnamed);
    }

    private static string NameOf(Type genericType) => genericType.Name[..genericType.Name.IndexOf('`', StringComparison.Ordinal)];

    private static bool DerivesFrom(Type type, Type genericBase)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBase)
            {
                return true;
            }
        }

        return false;
    }
}
