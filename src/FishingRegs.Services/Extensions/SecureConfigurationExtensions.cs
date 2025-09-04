using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace FishingRegs.Services.Extensions;

/// <summary>
/// Configuration extensions for secure credential management
/// </summary>
public static class SecureConfigurationExtensions
{
    /// <summary>
    /// Adds Azure Key Vault configuration to the configuration builder
    /// </summary>
    /// <param name="configurationBuilder">Configuration builder</param>
    /// <param name="keyVaultUri">Key Vault URI</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAzureKeyVaultSecrets(
        this IConfigurationBuilder configurationBuilder,
        string keyVaultUri)
    {
        if (string.IsNullOrWhiteSpace(keyVaultUri))
        {
            return configurationBuilder;
        }

        // Use DefaultAzureCredential which tries multiple authentication methods:
        // 1. Environment variables (for CI/CD)
        // 2. Managed Identity (for Azure-hosted services)
        // 3. Visual Studio (for local development)
        // 4. Azure CLI (for local development)
        // 5. Interactive browser (fallback)
        var credential = new DefaultAzureCredential();

        configurationBuilder.AddAzureKeyVault(
            new Uri(keyVaultUri),
            credential);

        return configurationBuilder;
    }

    /// <summary>
    /// Adds user secrets for local development
    /// </summary>
    /// <param name="configurationBuilder">Configuration builder</param>
    /// <param name="userSecretsId">User secrets ID</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddUserSecretsIfDevelopment(
        this IConfigurationBuilder configurationBuilder,
        string userSecretsId)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                         ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                         ?? "Production";

        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddUserSecrets(userSecretsId);
        }

        return configurationBuilder;
    }

    /// <summary>
    /// Creates a secure configuration builder with multiple sources
    /// </summary>
    /// <param name="userSecretsId">User secrets ID for development</param>
    /// <param name="keyVaultUri">Key Vault URI for production</param>
    /// <returns>Configuration with secure credential sources</returns>
    public static IConfiguration BuildSecureConfiguration(
        string userSecretsId,
        string? keyVaultUri = null)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", 
                        optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecretsIfDevelopment(userSecretsId);

        // Add Key Vault if URI is provided
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.AddAzureKeyVaultSecrets(keyVaultUri);
        }

        return builder.Build();
    }
}
