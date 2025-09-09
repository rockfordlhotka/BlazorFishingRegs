using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;
using FishingRegs.Data.Extensions;
using FishingRegs.Data;
using FishingRegs.Data.Models;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Comprehensive data ingestion test - systematically extracts and populates ALL data
/// 1. Get a list of counties
/// 2. Get a list of lakes, rivers, and streams (with county)
/// 3. Get a list of all types of restricted fish
/// 4. Get a list of all restrictions per water body
/// </summary>
class ComprehensiveDataIngestionProgram
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    /// <summary>
    /// Demonstrates comprehensive data extraction and population using AI
    /// This approach systematically extracts ALL data from the document
    /// </summary>
    public static async Task RunComprehensiveIngestion(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Comprehensive Fishing Regulations Data Ingestion", style: "bold"))
                .BorderColor(Color.Blue)
                .Header("[yellow]AI-Powered Systematic Extraction[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[cyan]This systematically extracts ALL data using AI:[/]");
        AnsiConsole.MarkupLine("[cyan]1. Counties ? 2. Fish Species ? 3. Water Bodies ? 4. Regulations[/]");
        AnsiConsole.WriteLine();

        // Setup dependency injection with secure configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        try
        {
            // Get Key Vault URI from environment or arguments (optional for production)
            var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URI");
            
            // Display configuration status
            var configTable = new Table()
                .AddColumn("Configuration")
                .AddColumn("Status");
            
            configTable.AddRow("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
            configTable.AddRow("Key Vault", !string.IsNullOrWhiteSpace(keyVaultUri) ? "[green]Enabled[/]" : "[red]Disabled[/]");
            configTable.AddRow("User Secrets", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true ? "[green]Enabled[/]" : "[red]Disabled[/]");
            configTable.AddRow("Processing Mode", "[blue]COMPREHENSIVE (Systematic AI)[/]");
            
            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            // Register text processing services with secure configuration
            services.AddTextProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            // Add data access services
            var configuration = BuildConfiguration();
            services.AddDataAccessLayer(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<ComprehensiveDataIngestionProgram>>();
            
            logger.LogInformation("Starting comprehensive data ingestion...");

            // Get services
            var comprehensivePopulationService = serviceProvider.GetRequiredService<IComprehensiveDatabasePopulationService>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

            // Ensure required reference data exists
            await EnsureMinnesotaStateExists(unitOfWork, logger);

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]? Test file not found:[/] {testTextPath}");
                return;
            }

            // Create a source document record
            var sourceDocument = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                FileName = "fishing_regs.txt",
                OriginalFileName = "fishing_regs.txt",
                DocumentType = "fishing_regulations",
                MimeType = "text/plain",
                FileSizeBytes = new FileInfo(testTextPath).Length,
                BlobStorageUrl = testTextPath,
                BlobContainer = "test-documents",
                StateId = 1, // Minnesota
                RegulationYear = DateTime.Now.Year,
                UploadSource = "manual",
                ProcessingStatus = "processing",
                ProcessingStartedAt = DateTimeOffset.UtcNow,
                ProcessingCompletedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // Add the document to database
            await unitOfWork.RegulationDocuments.AddAsync(sourceDocument);
            await unitOfWork.SaveChangesAsync();

            AnsiConsole.MarkupLine($"[green]? Created source document record:[/] {sourceDocument.Id}");

            // Read the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);

            // Comprehensive Processing: Extract ALL Data Systematically
            AnsiConsole.Write(new Rule("[blue]Comprehensive AI Extraction: Systematic Data Population[/]"));
            AnsiConsole.MarkupLine("[blue]Using AI to extract ALL counties, fish species, water bodies, and regulations...[/]");

            // Use the comprehensive population service
            var populationResult = await comprehensivePopulationService.PopulateAllDataAsync(
                textContent, 
                sourceDocument.Id, 
                DateTime.Now.Year);

            if (!populationResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]? Comprehensive population failed:[/] {populationResult.ErrorMessage}");
                return;
            }

            AnsiConsole.MarkupLine("[green]? Comprehensive data population completed![/]");
            
            // Create results table
            var resultsTable = new Table()
                .AddColumn("Data Type")
                .AddColumn("Items Processed");
            
            resultsTable.AddRow("Counties (from extraction)", populationResult.CountiesProcessed.ToString());
            resultsTable.AddRow("Counties (created on-demand)", populationResult.CountiesCreatedDuringProcessing.ToString());
            resultsTable.AddRow("Fish Species", populationResult.FishSpeciesProcessed.ToString());
            resultsTable.AddRow("Water Bodies", populationResult.WaterBodiesProcessed.ToString());
            resultsTable.AddRow("Regulations", populationResult.RegulationsProcessed.ToString());
            resultsTable.AddRow("Total Items", populationResult.TotalItemsProcessed.ToString());
            resultsTable.AddRow("Processing Time", $"{populationResult.ProcessingTime.TotalSeconds:F2} seconds");
            resultsTable.AddRow("Processing Mode", "[blue]COMPREHENSIVE (Systematic AI)[/]");
            
            AnsiConsole.Write(resultsTable);

            if (populationResult.CountiesCreatedOnDemand.Any())
            {
                AnsiConsole.MarkupLine("\n[cyan]? Counties created on-demand during processing:[/]");
                foreach (var county in populationResult.CountiesCreatedOnDemand)
                {
                    AnsiConsole.MarkupLine($"  [cyan]• {county}[/]");
                }
            }

            if (populationResult.ProcessingWarnings.Any())
            {
                AnsiConsole.MarkupLine("\n[yellow]?? Processing warnings:[/]");
                foreach (var warning in populationResult.ProcessingWarnings.Take(5))
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning}[/]");
                }
            }

            // Final verification
            AnsiConsole.Write(new Rule("[blue]Database Verification[/]"));
            var totalCounties = await unitOfWork.Counties.CountAsync(c => c.StateId == 1);
            var totalFishSpecies = await unitOfWork.FishSpecies.CountAsync(fs => fs.IsActive);
            var totalWaterBodies = await unitOfWork.WaterBodies.CountAsync(wb => wb.IsActive);
            var totalRegulations = await unitOfWork.FishingRegulations.CountAsync(fr => fr.IsActive);

            AnsiConsole.MarkupLine($"[green]? Final database counts:[/]");
            AnsiConsole.MarkupLine($"  - Minnesota counties: {totalCounties}");
            AnsiConsole.MarkupLine($"  - Active fish species: {totalFishSpecies}");
            AnsiConsole.MarkupLine($"  - Active water bodies: {totalWaterBodies}");
            AnsiConsole.MarkupLine($"  - Active fishing regulations: {totalRegulations}");

            // Show some sample data
            await ShowSampleData(unitOfWork);

            // Final success message
            AnsiConsole.Write(
                new Panel(new Text("?? Comprehensive Data Ingestion Completed Successfully! ??", style: "bold green"))
                    .BorderColor(Color.Green)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine("[green]The AI has systematically extracted and populated ALL data from the fishing regulations document![/]");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("configuration") || ex.Message.Contains("User Secrets") || ex.Message.Contains("Key Vault"))
        {
            AnsiConsole.Write(
                new Panel(new Text("Configuration Error", style: "bold red"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine($"[red]? Configuration Error:[/] {ex.Message}");
            
            AnsiConsole.Write(
                new Panel(new Markup("[yellow]?? Secure Configuration Setup Required[/]\n\n" +
                    "[dim]For DEVELOPMENT (User Secrets):[/]\n" +
                    "[cyan]Run these commands in the FishingRegs.TestConsole directory:[/]\n\n" +
                    "[grey]dotnet user-secrets set \"AzureAI:OpenAI:Endpoint\" \"https://your-openai.openai.azure.com/\"[/]\n" +
                    "[grey]dotnet user-secrets set \"AzureAI:OpenAI:ApiKey\" \"your-api-key\"[/]\n" +
                    "[grey]dotnet user-secrets set \"AzureAI:OpenAI:DeploymentName\" \"your-deployment-name\"[/]\n" +
                    "[grey]dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"your-database-connection-string\"[/]"))
                .BorderColor(Color.Yellow)
                .Padding(1, 0));
        }
        catch (Exception ex)
        {
            AnsiConsole.Write(
                new Panel(new Text("Unexpected Error", style: "bold red"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine($"[red]? Unexpected error:[/] {ex.Message}");
            
            // Safely display stack trace without markup parsing issues
            var stackTrace = ex.StackTrace?.Replace("<", "").Replace(">", "") ?? "No stack trace available";
            AnsiConsole.MarkupLine($"[dim]Stack trace: {stackTrace}[/]");
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        if (isDevelopment)
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    private static async Task EnsureMinnesotaStateExists(IUnitOfWork unitOfWork, ILogger logger)
    {
        logger.LogInformation("Checking for Minnesota state record...");

        // Check if Minnesota state exists
        var minnesotaState = await unitOfWork.States.GetByIdAsync(1);
        if (minnesotaState == null)
        {
            AnsiConsole.MarkupLine("[yellow]?? Creating Minnesota state record...[/]");
            
            // Create Minnesota state
            var newState = new State
            {
                Id = 1,
                Name = "Minnesota",
                Code = "MN",
                Country = "US",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            
            await unitOfWork.States.AddAsync(newState);
            await unitOfWork.SaveChangesAsync();
            
            AnsiConsole.MarkupLine("[green]? Created Minnesota state record[/]");
            logger.LogInformation("Created Minnesota state record with ID 1");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]? Minnesota state record exists[/]");
            logger.LogInformation("Minnesota state record already exists");
        }
    }

    private static async Task ShowSampleData(IUnitOfWork unitOfWork)
    {
        AnsiConsole.Write(new Rule("[blue]Sample Data Preview[/]"));

        // Show sample counties
        var sampleCounties = await unitOfWork.Counties.GetByStateAsync(1);
        var countiesList = sampleCounties.Take(5).Select(c => c.Name).ToList();

        if (countiesList.Any())
        {
            AnsiConsole.MarkupLine("[cyan]Sample Counties:[/]");
            foreach (var county in countiesList)
            {
                AnsiConsole.MarkupLine($"  • {county}");
            }
        }

        // Show sample fish species
        var allFishSpecies = await unitOfWork.FishSpecies.GetAllAsync();
        var sampleSpecies = allFishSpecies.Where(fs => fs.IsActive).Take(5).Select(fs => fs.CommonName).ToList();

        if (sampleSpecies.Any())
        {
            AnsiConsole.MarkupLine("\n[cyan]Sample Fish Species:[/]");
            foreach (var species in sampleSpecies)
            {
                AnsiConsole.MarkupLine($"  • {species}");
            }
        }

        // Show sample water bodies
        var waterBodiesWithRelated = await unitOfWork.WaterBodies.GetWithRelatedDataAsync();
        var sampleWaterBodies = waterBodiesWithRelated.Take(5).Select(wb => new { 
            wb.Name, 
            wb.WaterType, 
            CountyName = wb.County?.Name ?? "Unknown" 
        }).ToList();

        if (sampleWaterBodies.Any())
        {
            AnsiConsole.MarkupLine("\n[cyan]Sample Water Bodies:[/]");
            foreach (var wb in sampleWaterBodies)
            {
                AnsiConsole.MarkupLine($"  • {wb.Name} ({wb.WaterType}) - {wb.CountyName} County");
            }
        }
    }
}