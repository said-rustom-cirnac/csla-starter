using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace CslaStarter.Infrastructure.Database;

/// <summary>
/// Azure Database for PostgreSQL with Entra ID authentication: the app's managed identity gets a token instead of a
/// password (Database:UseEntraAuthentication = true). Tokens last about an hour; Npgsql refreshes them in the background.
/// </summary>
public static class EntraDatabaseAuthentication
{
    private static readonly TokenRequestContext TokenRequest = new(["https://ossrdbms-aad.database.windows.net/.default"]);

    public static void Configure(NpgsqlDataSourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var credential = new DefaultAzureCredential();
        builder.UsePeriodicPasswordProvider(
            async (_, cancellationToken) => (await credential.GetTokenAsync(TokenRequest, cancellationToken)).Token,
            successRefreshInterval: TimeSpan.FromMinutes(50),
            failureRefreshInterval: TimeSpan.FromSeconds(10));
    }
}
