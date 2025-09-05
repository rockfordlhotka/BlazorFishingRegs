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
/// Complete test program for Section 3.2: Text upload -> AI extraction -> Database population
/// </summary>
class DatabasePopulationTestProgram
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task MainDatabase(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Fishing Regulations Database Population Test", style: "bold"))
                .BorderColor(Color.Green)
                .Header("[yellow]Section 3.2[/]")
                .Padding(1, 0));

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
            
            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            // Register text processing services with secure configuration
            services.AddTextProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            // Add data access services
            var configuration = BuildConfiguration();
            services.AddDataAccessLayer(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabasePopulationTestProgram>>();
            
            logger.LogInformation("Starting Section 3.2 database population test...");

            // Get services
            var aiExtractionService = serviceProvider.GetRequiredService<IAiLakeRegulationExtractionService>();
            var databasePopulationService = serviceProvider.GetRequiredService<IRegulationDatabasePopulationService>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                logger.LogError("Test text file not found at: {FilePath}", testTextPath);
                AnsiConsole.MarkupLine($"[red]❌ Test text file not found at:[/] {testTextPath}");
                AnsiConsole.MarkupLine("[yellow]Please ensure the fishing_regs.txt file exists in the data folder.[/]");
                return;
            }

            logger.LogInformation("Found test text file at: {FilePath}", testTextPath);
            Console.WriteLine($"Processing text file: {testTextPath}\n");

            // Create a test regulation document record
            var sourceDocument = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                FileName = "fishing_regs.txt",
                OriginalFileName = "fishing_regs.txt",
                DocumentType = "text",
                ProcessingStatus = "completed",
                FileSizeBytes = new FileInfo(testTextPath).Length,
                MimeType = "text/plain",
                BlobStorageUrl = $"test://{Path.GetFileName(testTextPath)}",
                BlobContainer = "test-container",
                StateId = 1, // Minnesota
                RegulationYear = DateTime.Now.Year,
                UploadSource = "test",
                ProcessingStartedAt = DateTimeOffset.UtcNow,
                ProcessingCompletedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // Add the document to database
            await unitOfWork.RegulationDocuments.AddAsync(sourceDocument);
            await unitOfWork.SaveChangesAsync();

            AnsiConsole.MarkupLine($"[green]✅ Created source document record:[/] {sourceDocument.Id}");

            // Read and process the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);

            // Step 1: AI Extraction
            AnsiConsole.Write(new Rule("[blue]Step 1: AI Extraction[/]"));
            AnsiConsole.MarkupLine("[blue]Extracting lake regulations using AI...[/]");
            
            var extractionResult = await AnsiConsole.Status()
                .Start("Processing with Azure OpenAI...", async ctx => 
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    return await aiExtractionService.ExtractLakeRegulationsAsync(textContent);
                });

            if (!extractionResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]❌ AI extraction failed:[/] {extractionResult.ErrorMessage}");
                return;
            }

            AnsiConsole.MarkupLine("[green]✅ AI extraction completed successfully![/]");
            
            // Create results table
            var resultsTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Value");
            
            resultsTable.AddRow("Total lakes processed", extractionResult.TotalLakesProcessed.ToString());
            resultsTable.AddRow("Regulations extracted", extractionResult.TotalRegulationsExtracted.ToString());
            resultsTable.AddRow("Processing time", $"{extractionResult.ProcessingTime.TotalSeconds:F2} seconds");
            
            if (extractionResult.ProcessingWarnings.Any())
            {
                resultsTable.AddRow("Warnings", extractionResult.ProcessingWarnings.Count.ToString());
            }
            
            AnsiConsole.Write(resultsTable);

            if (extractionResult.ProcessingWarnings.Any())
            {
                AnsiConsole.MarkupLine("\n[yellow]⚠️ Warnings:[/]");
                foreach (var warning in extractionResult.ProcessingWarnings.Take(3))
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning}[/]");
                }
            }

            // Show sample extracted data
            AnsiConsole.MarkupLine("\n[cyan]📋 Sample extracted regulations:[/]");
            foreach (var lake in extractionResult.ExtractedRegulations.Take(3))
            {
                AnsiConsole.MarkupLine($"  [cyan]• {lake.LakeName}[/] ([dim]{lake.County}[/]): [green]{lake.Regulations.SpecialRegulations.Count}[/] special regulations");
                foreach (var regulation in lake.Regulations.SpecialRegulations.Take(2))
                {
                    AnsiConsole.MarkupLine($"    [dim]- {regulation.Species}: {regulation.RegulationType} ({regulation.Notes})[/]");
                }
            }

            // Step 2: Database Population
            AnsiConsole.Write(new Rule("[blue]Step 2: Database Population[/]"));
            AnsiConsole.MarkupLine("[blue]Populating database with extracted regulations...[/]");
            
            var populationResult = await AnsiConsole.Status()
                .Start("Writing to database...", async ctx => 
                {
                    ctx.Spinner(Spinner.Known.Arc);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    return await databasePopulationService.PopulateDatabaseAsync(
                        extractionResult, 
                        sourceDocument.Id, 
                        DateTime.Now.Year);
                });

            if (!populationResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]❌ Database population failed:[/] {populationResult.ErrorMessage}");
                
                if (populationResult.ProcessingErrors.Any())
                {
                    AnsiConsole.MarkupLine("[red]Processing errors:[/]");
                    foreach (var error in populationResult.ProcessingErrors.Take(5))
                    {
                        AnsiConsole.MarkupLine($"  [red]❌ {error}[/]");
                    }
                }
                return;
            }

            AnsiConsole.MarkupLine("[green]✅ Database population completed successfully![/]");
            
            // Create results table for database population
            var dbResultsTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Count");
            
            dbResultsTable.AddRow("Total lakes processed", populationResult.TotalLakesProcessed.ToString());
            dbResultsTable.AddRow("Water bodies created", populationResult.WaterBodiesCreated.ToString());
            dbResultsTable.AddRow("Water bodies updated", populationResult.WaterBodiesUpdated.ToString());
            dbResultsTable.AddRow("Regulations created", populationResult.RegulationsCreated.ToString());
            dbResultsTable.AddRow("Regulations updated", populationResult.RegulationsUpdated.ToString());
            dbResultsTable.AddRow("Fish species created", populationResult.FishSpeciesCreated.ToString());
            dbResultsTable.AddRow("Processing time", $"{populationResult.ProcessingTime.TotalSeconds:F2} seconds");
            
            AnsiConsole.Write(dbResultsTable);

            if (populationResult.ProcessingWarnings.Any())
            {
                AnsiConsole.MarkupLine("\n[yellow]⚠️ Processing warnings:[/]");
                foreach (var warning in populationResult.ProcessingWarnings.Take(3))
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning}[/]");
                }
            }

            // Step 3: Verify database contents
            AnsiConsole.Write(new Rule("[blue]Step 3: Database Verification[/]"));
            AnsiConsole.MarkupLine("[blue]Verifying database contents...[/]");

            var totalWaterBodies = await unitOfWork.WaterBodies.CountAsync(wb => wb.IsActive);
            var totalRegulations = await unitOfWork.FishingRegulations.CountAsync(fr => fr.IsActive);
            var totalFishSpecies = await unitOfWork.FishSpecies.CountAsync(fs => fs.IsActive);

            Console.WriteLine($"✅ Database verification:");
            Console.WriteLine($"  - Total active water bodies: {totalWaterBodies}");
            Console.WriteLine($"  - Total active fishing regulations: {totalRegulations}");
            Console.WriteLine($"  - Total active fish species: {totalFishSpecies}");

            // Show sample database records
            var sampleWaterBodies = await unitOfWork.WaterBodies.GetAllAsync();
            Console.WriteLine("\n  Sample water bodies:");
            foreach (var waterBody in sampleWaterBodies.Take(5))
            {
                var regulationCount = await unitOfWork.FishingRegulations.CountAsync(fr => 
                    fr.WaterBodyId == waterBody.Id && fr.IsActive);
                Console.WriteLine($"    {waterBody.Name} (ID: {waterBody.Id}): {regulationCount} regulations");
            }

            var sampleSpecies = await unitOfWork.FishSpecies.GetAllAsync();
            AnsiConsole.MarkupLine("\n[cyan]🐟 Sample fish species:[/]");
            foreach (var species in sampleSpecies.Take(5))
            {
                var regulationCount = await unitOfWork.FishingRegulations.CountAsync(fr => 
                    fr.SpeciesId == species.Id && fr.IsActive);
                AnsiConsole.MarkupLine($"  [cyan]• {species.CommonName}[/] (ID: {species.Id}): [green]{regulationCount}[/] regulations");
            }

            // Step 4: Test specific queries
            AnsiConsole.Write(new Rule("[blue]Step 4: Testing Queries[/]"));
            AnsiConsole.MarkupLine("[blue]Testing regulation queries...[/]");

            if (totalWaterBodies > 0 && totalFishSpecies > 0)
            {
                var firstWaterBody = sampleWaterBodies.First();
                var regulations = await unitOfWork.FishingRegulations.GetByWaterBodyAsync(firstWaterBody.Id);
                
                AnsiConsole.MarkupLine($"[green]✅ Query test for {firstWaterBody.Name}:[/]");
                AnsiConsole.MarkupLine($"  [dim]Found {regulations.Count()} regulations[/]");
                
                foreach (var regulation in regulations.Take(3))
                {
                    AnsiConsole.MarkupLine($"    [dim]- Species ID {regulation.SpeciesId}: Daily limit {regulation.DailyLimit}, " +
                                    $"Min size {regulation.MinimumSizeInches}\"[/]");
                }
            }

            // Final success message
            AnsiConsole.Write(
                new Panel(new Text("🎉 Pipeline Test Completed Successfully! 🎉", style: "bold green"))
                    .BorderColor(Color.Green)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine("[green]Text upload → AI extraction → Database population pipeline is working![/]");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("configuration") || ex.Message.Contains("User Secrets") || ex.Message.Contains("Key Vault"))
        {
            AnsiConsole.Write(
                new Panel(new Text("Configuration Error", style: "bold red"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine($"[red]❌ Configuration Error:[/] {ex.Message}");
            
            AnsiConsole.Write(
                new Panel(new Markup("[yellow]🔐 Secure Configuration Setup Required[/]\n\n" +
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
                    
            AnsiConsole.MarkupLine($"[red]❌ Unexpected error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Stack trace: {ex.StackTrace}[/]");
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
}
