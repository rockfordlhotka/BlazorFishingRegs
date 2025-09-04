using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Services;

namespace FishingRegs.Services.Extensions;

/// <summary>
/// Service collection extensions for registering PDF processing services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PDF processing services with the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPdfProcessingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Azure Document Intelligence service
        services.AddSingleton<IAzureDocumentIntelligenceService, AzureDocumentIntelligenceService>();

        // Register Blob Storage service
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        // Register PDF Splitting service
        services.AddScoped<IPdfSplittingService, PdfSplittingService>();

        // Register PDF Processing service
        services.AddScoped<IPdfProcessingService, PdfProcessingService>();

        return services;
    }

    /// <summary>
    /// Registers PDF processing services with secure configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="userSecretsId">User secrets ID for development</param>
    /// <param name="keyVaultUri">Key Vault URI for production (optional)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPdfProcessingServicesWithSecureConfig(
        this IServiceCollection services,
        string userSecretsId,
        string? keyVaultUri = null)
    {
        // Build secure configuration
        var secureConfiguration = SecureConfigurationExtensions.BuildSecureConfiguration(
            userSecretsId, keyVaultUri);

        // Register the secure configuration
        services.AddSingleton<IConfiguration>(secureConfiguration);

        // Validate configuration
        secureConfiguration.ValidatePdfProcessingConfiguration();

        // Register PDF processing services
        return services.AddPdfProcessingServices(secureConfiguration);
    }

    /// <summary>
    /// Validates that required configuration settings are present
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing</exception>
    public static void ValidatePdfProcessingConfiguration(this IConfiguration configuration)
    {
        var requiredSettings = new[]
        {
            "AzureAI:DocumentIntelligence:Endpoint",
            "AzureAI:DocumentIntelligence:ApiKey",
            "ConnectionStrings:AzureStorage"
        };

        var missingSettings = requiredSettings
            .Where(setting => string.IsNullOrWhiteSpace(configuration[setting]))
            .ToList();

        if (missingSettings.Any())
        {
            throw new InvalidOperationException(
                $"Missing required configuration settings: {string.Join(", ", missingSettings)}. " +
                "Please configure these in User Secrets (development) or Azure Key Vault (production).");
        }
    }

    /// <summary>
    /// Gets configuration values with secure fallback
    /// </summary>
    /// <param name="configuration">Configuration</param>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value or null if not found</returns>
    public static string? GetSecureValue(this IConfiguration configuration, string key)
    {
        // Try to get from configuration (includes Key Vault, User Secrets, etc.)
        var value = configuration[key];
        
        if (string.IsNullOrWhiteSpace(value))
        {
            // Try alternative key format (replace : with __)
            var alternativeKey = key.Replace(":", "__");
            value = configuration[alternativeKey];
        }

        return value;
    }
}
