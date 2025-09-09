using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FishingRegs.Data.Extensions;
using FishingRegs.Data;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Database cleaner utility - removes all data from database tables while preserving schema
/// </summary>
class DatabaseCleaner
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task ClearDatabase(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Database Cleaner", style: "bold"))
                .BorderColor(Color.Red)
                .Header("[yellow]Clear All Data[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[red]??  WARNING: This will remove ALL data from the database![/]");
        AnsiConsole.MarkupLine("[dim]The database schema will be preserved, but all records will be deleted.[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Build configuration
            var configuration = BuildConfiguration();

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddDataAccessLayer(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabaseCleaner>>();
            var dbContext = serviceProvider.GetRequiredService<FishingRegsDbContext>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

            // Get connection info
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                AnsiConsole.Write(
                    new Panel(new Markup("[red]? No database connection string found.[/]\n\n" +
                        "[yellow]Please set up user secrets:[/]\n" +
                        "[grey]dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"your-connection-string\"[/]"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                return;
            }

            AnsiConsole.MarkupLine("[green]? Database connection string found[/]");
            AnsiConsole.MarkupLine($"[dim]Connection: {MaskConnectionString(connectionString)}[/]");
            AnsiConsole.WriteLine();

            // Show current data counts
            await ShowCurrentDataCounts(unitOfWork);

            // Confirm deletion
            if (!AnsiConsole.Confirm("\n[red]Are you sure you want to clear ALL data from the database?[/]"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return;
            }

            // Double confirmation for safety
            if (!AnsiConsole.Confirm("[red]This action cannot be undone. Are you absolutely sure?[/]"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return;
            }

            logger.LogInformation("Starting database clear operation...");

            // Clear data in proper order (respecting foreign key constraints)
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[red]Clearing database[/]");
                    task.MaxValue = 8;

                    // Delete in reverse dependency order
                    AnsiConsole.MarkupLine("[yellow]Clearing fishing regulations...[/]");
                    await dbContext.FishingRegulations.ExecuteDeleteAsync();
                    task.Increment(1);

                    AnsiConsole.MarkupLine("[yellow]Clearing regulation documents...[/]");
                    await dbContext.RegulationDocuments.ExecuteDeleteAsync();
                    task.Increment(1);

                    AnsiConsole.MarkupLine("[yellow]Clearing water bodies...[/]");
                    await dbContext.WaterBodies.ExecuteDeleteAsync();
                    task.Increment(1);

                    AnsiConsole.MarkupLine("[yellow]Clearing fish species...[/]");
                    await dbContext.FishSpecies.ExecuteDeleteAsync();
                    task.Increment(1);

                    AnsiConsole.MarkupLine("[yellow]Clearing counties...[/]");
                    await dbContext.Counties.ExecuteDeleteAsync();
                    task.Increment(1);

                    AnsiConsole.MarkupLine("[yellow]Clearing states...[/]");
                    await dbContext.States.ExecuteDeleteAsync();
                    task.Increment(1);

                    // Reset any sequences/identity columns if using PostgreSQL
                    if (connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                    {
                        AnsiConsole.MarkupLine("[yellow]Resetting sequences...[/]");
                        await ResetPostgreSqlSequences(dbContext);
                    }
                    task.Increment(1);

                    // Save changes
                    AnsiConsole.MarkupLine("[yellow]Finalizing changes...[/]");
                    await dbContext.SaveChangesAsync();
                    task.Increment(1);
                });

            AnsiConsole.MarkupLine("[green]? Database cleared successfully![/]");

            // Show final data counts (should all be zero)
            AnsiConsole.WriteLine();
            await ShowCurrentDataCounts(unitOfWork);

            AnsiConsole.Write(
                new Panel(new Text("?? Database Clear Completed Successfully! ??", style: "bold green"))
                    .BorderColor(Color.Green)
                    .Padding(1, 0));

            AnsiConsole.MarkupLine("\n[cyan]?? Next Steps:[/]");
            AnsiConsole.MarkupLine("[dim]• The database has been cleared of all data[/]");
            AnsiConsole.MarkupLine("[dim]• Reference data (states, counties) will be automatically created when you run data ingestion[/]");
            AnsiConsole.MarkupLine("[dim]• Consider running 'Streaming Data Ingestion' or 'Mock Data Population Test' next[/]");

            logger.LogInformation("Database clear operation completed successfully");
        }
        catch (Exception ex)
        {
            AnsiConsole.Write(
                new Panel(new Text("Database Clear Error", style: "bold red"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));

            AnsiConsole.MarkupLine($"[red]? Error clearing database:[/] {ex.Message}");
            
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[red]Inner Error:[/] {ex.InnerException.Message}");
            }
            
            AnsiConsole.MarkupLine($"[dim]Stack trace: {ex.StackTrace}[/]");
        }

        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    private static async Task ShowCurrentDataCounts(IUnitOfWork unitOfWork)
    {
        var table = new Table()
            .AddColumn("Table")
            .AddColumn("Record Count");

        var stateCount = await unitOfWork.States.CountAsync();
        var countyCount = await unitOfWork.Counties.CountAsync();
        var waterBodyCount = await unitOfWork.WaterBodies.CountAsync();
        var fishSpeciesCount = await unitOfWork.FishSpecies.CountAsync();
        var regulationDocumentCount = await unitOfWork.RegulationDocuments.CountAsync();
        var fishingRegulationCount = await unitOfWork.FishingRegulations.CountAsync();

        table.AddRow("States", stateCount.ToString());
        table.AddRow("Counties", countyCount.ToString());
        table.AddRow("Water Bodies", waterBodyCount.ToString());
        table.AddRow("Fish Species", fishSpeciesCount.ToString());
        table.AddRow("Regulation Documents", regulationDocumentCount.ToString());
        table.AddRow("Fishing Regulations", fishingRegulationCount.ToString());

        var totalRecords = stateCount + countyCount + waterBodyCount + fishSpeciesCount + 
                          regulationDocumentCount + fishingRegulationCount;
        
        table.AddEmptyRow();
        table.AddRow("[bold]Total Records[/]", $"[bold]{totalRecords}[/]");

        AnsiConsole.Write(table);
    }

    private static async Task ResetPostgreSqlSequences(FishingRegsDbContext dbContext)
    {
        try
        {
            // Check if we're actually using PostgreSQL
            var isPostgreSql = dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
            
            if (!isPostgreSql)
            {
                AnsiConsole.MarkupLine("[dim]Skipping sequence reset (not PostgreSQL)[/]");
                return;
            }

            // Reset sequences for tables with identity columns
            var sequenceResetQueries = new[]
            {
                "ALTER SEQUENCE IF EXISTS states_id_seq RESTART WITH 1;",
                "ALTER SEQUENCE IF EXISTS counties_id_seq RESTART WITH 1;",
                "ALTER SEQUENCE IF EXISTS water_bodies_id_seq RESTART WITH 1;",
                "ALTER SEQUENCE IF EXISTS fish_species_id_seq RESTART WITH 1;",
                "ALTER SEQUENCE IF EXISTS fishing_regulations_id_seq RESTART WITH 1;"
            };

            foreach (var query in sequenceResetQueries)
            {
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(query);
                }
                catch (Exception ex)
                {
                    // Log but don't fail - sequence might not exist or be named differently
                    AnsiConsole.MarkupLine($"[dim]Note: Could not reset sequence: {ex.Message}[/]");
                }
            }
            
            AnsiConsole.MarkupLine("[green]? Sequences reset successfully[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]?? Warning: Could not reset sequences: {ex.Message}[/]");
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask sensitive parts of the connection string for display
        var parts = connectionString.Split(';');
        var maskedParts = new List<string>();
        
        foreach (var part in parts)
        {
            if (part.ToLowerInvariant().Contains("password"))
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    maskedParts.Add($"{keyValue[0]}=***");
                }
                else
                {
                    maskedParts.Add("Password=***");
                }
            }
            else
            {
                maskedParts.Add(part);
            }
        }
        
        return string.Join(";", maskedParts);
    }
}