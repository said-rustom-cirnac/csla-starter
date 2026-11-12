using Azure.Identity;
using CslaStarter.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CslaStarter.Infrastructure.DataProtection;

public static class DataProtectionSetup
{
    public const string ApplicationName = "CslaStarter";

    /// <summary>
    /// Data Protection keys live in PostgreSQL so every instance shares them, and must be encrypted at rest outside
    /// Development: a certificate (on-premises IIS) or an Azure Key Vault key (App Service).
    /// </summary>
    public static IServiceCollection AddCslaStarterDataProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var builder = services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<AppDbContext>();

        var thumbprint = configuration["DataProtection:CertificateThumbprint"];
        var keyVaultKeyId = configuration["DataProtection:KeyVaultKeyId"];

        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            builder.ProtectKeysWithCertificate(thumbprint);
        }
        else if (!string.IsNullOrWhiteSpace(keyVaultKeyId))
        {
            builder.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyId), new DefaultAzureCredential());
        }
        else if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "Data Protection keys must be protected at rest: set DataProtection:CertificateThumbprint or DataProtection:KeyVaultKeyId.");
        }

        return services;
    }
}
