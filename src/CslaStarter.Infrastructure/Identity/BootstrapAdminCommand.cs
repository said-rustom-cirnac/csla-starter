using CslaStarter.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// <c>CslaStarter.Web bootstrap-admin --tenant &lt;tid&gt; --object-id &lt;oid&gt; [--name &lt;name&gt;]</c>, run once by
/// the deployer with ConnectionStrings:Owner. Exit codes: 0 done, 1 bad arguments, 2 refused (an Admin exists).
/// </summary>
public static class BootstrapAdminCommand
{
    public const string Name = "bootstrap-admin";

    public static async Task<int> RunAsync(IConfiguration configuration, IReadOnlyList<string> args, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);

        var options = Parse(args);
        var owner = configuration.GetConnectionString("Owner");
        if (owner is null || !options.TryGetValue("--tenant", out var tenant) || !options.TryGetValue("--object-id", out var objectId))
        {
            await Console.Error.WriteLineAsync("Usage: bootstrap-admin --tenant <tid> --object-id <oid> [--name <name>] (needs ConnectionStrings:Owner)");
            return 1;
        }

        var bootstrapper = new AdminBootstrapper(new AppDatabaseConnection(owner), TimeProvider.System, loggerFactory);
        return await bootstrapper.RunAsync(tenant, objectId, options.GetValueOrDefault("--name", "Administrator"), cancellationToken);
    }

    private static Dictionary<string, string> Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            options[args[i]] = args[i + 1];
        }

        return options;
    }
}
