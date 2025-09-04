using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using FishingRegs.Data.Repositories;
using FishingRegs.Data.Repositories.Implementation;

namespace FishingRegs.Data.Extensions;

/// <summary>
/// Extension methods for registering data access services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the data access layer services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="connectionStringName">The name of the connection string (default: "DefaultConnection")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
    {
        // Add DbContext with PostgreSQL
        services.AddDbContext<FishingRegsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                
                // Enable array support for PostgreSQL lists
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            // Enable sensitive data logging in development
            if (configuration.GetValue<bool>("DetailedErrors"))
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Register repositories
        services.AddScoped<IWaterBodyRepository, WaterBodyRepository>();
        services.AddScoped<IFishingRegulationRepository, FishingRegulationRepository>();
        services.AddScoped<IRegulationDocumentRepository, RegulationDocumentRepository>();
        services.AddScoped<IStateRepository, StateRepository>();
        services.AddScoped<ICountyRepository, CountyRepository>();
        services.AddScoped<IFishSpeciesRepository, FishSpeciesRepository>();

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Adds the data access layer services with custom DbContext configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureDbContext">Action to configure the DbContext</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        // Add DbContext with custom configuration
        services.AddDbContext<FishingRegsDbContext>(configureDbContext);

        // Register repositories
        services.AddScoped<IWaterBodyRepository, WaterBodyRepository>();
        services.AddScoped<IFishingRegulationRepository, FishingRegulationRepository>();
        services.AddScoped<IRegulationDocumentRepository, RegulationDocumentRepository>();
        services.AddScoped<IStateRepository, StateRepository>();
        services.AddScoped<ICountyRepository, CountyRepository>();
        services.AddScoped<IFishSpeciesRepository, FishSpeciesRepository>();

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Adds database health checks for the application
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="healthCheckName">The name of the health check (default: "database")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDataAccessHealthChecks(
        this IServiceCollection services,
        string healthCheckName = "database")
    {
        services.AddHealthChecks()
            .AddDbContextCheck<FishingRegsDbContext>(healthCheckName);

        return services;
    }

    /// <summary>
    /// Ensures the database is created and migrations are applied
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task EnsureDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FishingRegsDbContext>();
        
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Seeds the database with initial data if it's empty
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FishingRegsDbContext>();
        
        // Check if data already exists
        if (await context.States.AnyAsync())
        {
            return; // Database already seeded
        }

        // Apply any pending migrations
        await context.Database.MigrateAsync();
        
        // The seeding is handled by the DbContext OnModelCreating method
        // This ensures the seed data is created if it doesn't exist
        await context.SaveChangesAsync();
    }
}
