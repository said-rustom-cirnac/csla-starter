using CslaStarter.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace CslaStarter.Integration.Tests;

/// <summary>The CSP's form-action allows the sign-in provider: sign-out posts to the app, which redirects there.</summary>
public sealed class SignInProviderTests
{
    [Theory]
    [InlineData(null, null, null, "https://login.microsoftonline.com")]
    [InlineData("Entra", "https://login.microsoftonline.us/", null, "https://login.microsoftonline.us")]
    [InlineData("MockOidc", null, "http://localhost:8080/entra", "http://localhost:8080")]
    public void ProviderOrigin_FollowsTheSignInMode(string? mode, string? instance, string? mockAuthority, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = mode,
            ["AzureAd:Instance"] = instance,
            ["Authentication:MockOidc:Authority"] = mockAuthority,
        }).Build();

        Assert.Equal(expected, AuthenticationSetup.ProviderOrigin(configuration));
    }
}
