using CslaStarter.Data.Platform;
using CslaStarter.Data.Platform.Texts;
using CslaStarter.Data.Platform.Users;
using CslaStarter.Domain.Platform.Texts;
using CslaStarter.Domain.Platform.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

/// <summary>The lifetimes <c>AddAppData</c> gives. Resolving only builds the services; nothing connects to the database.</summary>
public sealed class DataServicesTests
{
    [Theory]
    [InlineData(typeof(ITextDal))]
    [InlineData(typeof(IUserDirectory))]
    [InlineData(typeof(TextSeeder))]
    public void PlatformStore_IsASingleton_ResolvableFromTheRoot(Type store)
    {
        using var root = Root();
        Assert.Same(root.GetRequiredService(store), root.GetRequiredService(store));
    }

    [Fact]
    public void ScannedDal_IsScoped()
    {
        using var root = Root();
        Assert.Throws<InvalidOperationException>(() => root.GetRequiredService<IUserDal>());
        using var scope = root.CreateScope();
        Assert.IsType<UserDal>(scope.ServiceProvider.GetRequiredService<IUserDal>());
    }

    private static ServiceProvider Root() =>
        new ServiceCollection().AddAppData("Host=localhost;Database=unused").BuildServiceProvider(validateScopes: true);
}
