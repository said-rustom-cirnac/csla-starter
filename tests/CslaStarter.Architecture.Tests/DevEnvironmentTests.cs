using System.Text.Json;

namespace CslaStarter.Architecture.Tests;

/// <summary>The local mock sign-in server must issue every claim the app keys users on (tid + oid) plus a name.</summary>
public class DevEnvironmentTests
{
    [Theory]
    [InlineData("tid")]
    [InlineData("oid")]
    [InlineData("name")]
    public void MockOidcConfig_IssuesClaimTheAppNeeds(string claim)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "deploy", "mock-oidc", "mock-oidc.json")));

        var mappings = config.RootElement.GetProperty("tokenCallbacks").EnumerateArray()
            .SelectMany(callback => callback.GetProperty("requestMappings").EnumerateArray());

        Assert.All(mappings, mapping => Assert.True(
            mapping.GetProperty("claims").TryGetProperty(claim, out _),
            $"mock-oidc.json request mapping '{mapping.GetProperty("match")}' does not issue '{claim}'"));
    }
}
