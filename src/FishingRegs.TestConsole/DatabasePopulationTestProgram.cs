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
/// Streaming data ingestion test - processes each lake immediately after AI extraction
/// </summary>
class DatabasePopulationTestProgram
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    /// <summary>
    /// Demonstrates streaming processing where each lake's regulations are 
    /// extracted by AI and immediately updated in the database
    /// </summary>
    public static async Task MainDatabaseStream(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Fishing Regulations Streaming Data Ingestion", style: "bold"))
                .BorderColor(Color.Green)
                .Header("[yellow]Real-time Processing[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[cyan]This processes each lake immediately after AI extraction[/]");
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
            configTable.AddRow("Processing Mode", "[yellow]STREAMING (Real-time)[/]");
            
            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            // Register text processing services with secure configuration
            services.AddTextProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            // Add data access services
            var configuration = BuildConfiguration();
            services.AddDataAccessLayer(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabasePopulationTestProgram>>();
            
            logger.LogInformation("Starting streaming database population test...");

            // Get services
            var aiExtractionService = serviceProvider.GetRequiredService<IAiLakeRegulationExtractionService>();
            var databasePopulationService = serviceProvider.GetRequiredService<IRegulationDatabasePopulationService>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

            // Ensure required reference data exists
            await EnsureReferenceDataExists(unitOfWork, logger);

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]❌ Test file not found:[/] {testTextPath}");
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

            AnsiConsole.MarkupLine($"[green]✅ Created source document record:[/] {sourceDocument.Id}");

            // Read and process the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);

            // Streaming Processing: Extract and Process Each Lake Immediately
            AnsiConsole.Write(new Rule("[blue]Streaming Processing: Real-time AI Extraction + Database Updates[/]"));
            AnsiConsole.MarkupLine("[blue]Processing each lake immediately after AI extraction...[/]");

            var overallResult = new RegulationPopulationResult();
            var processedLakes = new List<string>();
            
            var streamingResult = await aiExtractionService.ExtractLakeRegulationsStreamAsync(
                textContent,
                async (lakeRegulation) =>
                {
                    // This callback is called immediately after each lake's AI extraction
                    AnsiConsole.MarkupLine($"[yellow]⚡ Processing {lakeRegulation.LakeName} in real-time...[/]");
                    
                    try
                    {
                        // Immediately populate database for this lake
                        var lakeResult = await databasePopulationService.PopulateSingleLakeAsync(
                            lakeRegulation, 
                            sourceDocument.Id, 
                            DateTime.Now.Year);

                        // Update overall statistics
                        if (lakeResult.IsSuccess)
                        {
                            if (lakeResult.WaterBody != null)
                            {
                                if (lakeResult.WaterBody.Id == 0) // New water body
                                    overallResult.WaterBodiesCreated++;
                                else
                                    overallResult.WaterBodiesUpdated++;
                            }

                            overallResult.RegulationsCreated += lakeResult.CreatedRegulations.Count;
                            overallResult.RegulationsUpdated += lakeResult.UpdatedRegulations.Count;
                            
                            // Save changes immediately for this lake
                            await unitOfWork.SaveChangesAsync();
                            
                            AnsiConsole.MarkupLine($"[green]✅ {lakeRegulation.LakeName} completed:[/] " +
                                $"{lakeResult.CreatedRegulations.Count} regulations created, " +
                                $"{lakeResult.UpdatedRegulations.Count} updated");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]❌ Failed to process {lakeRegulation.LakeName}:[/] {lakeResult.ErrorMessage}");
                            overallResult.ProcessingErrors.Add($"{lakeRegulation.LakeName}: {lakeResult.ErrorMessage}");
                        }

                        overallResult.ProcessingWarnings.AddRange(lakeResult.Warnings);
                        processedLakes.Add(lakeRegulation.LakeName);
                        overallResult.TotalLakesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]❌ Error processing {lakeRegulation.LakeName}:[/] {ex.Message}");
                        overallResult.ProcessingErrors.Add($"{lakeRegulation.LakeName}: {ex.Message}");
                    }
                });

            if (!streamingResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]❌ Streaming extraction failed:[/] {streamingResult.ErrorMessage}");
                return;
            }

            overallResult.IsSuccess = overallResult.ProcessingErrors.Count == 0;

            AnsiConsole.MarkupLine("[green]✅ Streaming processing completed![/]");
            
            // Create results table
            var resultsTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Value");
            
            resultsTable.AddRow("Total lakes processed", overallResult.TotalLakesProcessed.ToString());
            resultsTable.AddRow("Water bodies created", overallResult.WaterBodiesCreated.ToString());
            resultsTable.AddRow("Water bodies updated", overallResult.WaterBodiesUpdated.ToString());
            resultsTable.AddRow("Regulations created", overallResult.RegulationsCreated.ToString());
            resultsTable.AddRow("Regulations updated", overallResult.RegulationsUpdated.ToString());
            resultsTable.AddRow("Processing time", $"{streamingResult.ProcessingTime.TotalSeconds:F2} seconds");
            resultsTable.AddRow("Processing mode", "[yellow]STREAMING (Real-time)[/]");
            
            AnsiConsole.Write(resultsTable);

            if (overallResult.ProcessingWarnings.Any())
            {
                AnsiConsole.MarkupLine("\n[yellow]⚠️ Processing warnings:[/]");
                foreach (var warning in overallResult.ProcessingWarnings.Take(3))
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning}[/]");
                }
            }

            if (overallResult.ProcessingErrors.Any())
            {
                AnsiConsole.MarkupLine("\n[red]❌ Processing errors:[/]");
                foreach (var error in overallResult.ProcessingErrors.Take(3))
                {
                    AnsiConsole.MarkupLine($"  [red]• {error}[/]");
                }
            }

            // Final verification
            AnsiConsole.Write(new Rule("[blue]Verification[/]"));
            var totalWaterBodies = await unitOfWork.WaterBodies.CountAsync(wb => wb.IsActive);
            var totalRegulations = await unitOfWork.FishingRegulations.CountAsync(fr => fr.IsActive);
            var totalFishSpecies = await unitOfWork.FishSpecies.CountAsync(fs => fs.IsActive);

            AnsiConsole.MarkupLine($"[green]✅ Database verification:[/]");
            AnsiConsole.MarkupLine($"  - Total active water bodies: {totalWaterBodies}");
            AnsiConsole.MarkupLine($"  - Total active fishing regulations: {totalRegulations}");
            AnsiConsole.MarkupLine($"  - Total active fish species: {totalFishSpecies}");

            // Final success message
            AnsiConsole.Write(
                new Panel(new Text("🎉 Streaming Pipeline Test Completed Successfully! 🎉", style: "bold green"))
                    .BorderColor(Color.Green)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine("[green]Real-time processing: AI extraction → Immediate database update pipeline is working![/]");
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

    private static async Task EnsureReferenceDataExists(IUnitOfWork unitOfWork, ILogger logger)
    {
        logger.LogInformation("Checking for required reference data...");

        // Check if Minnesota state exists
        var minnesotaState = await unitOfWork.States.GetByIdAsync(1);
        if (minnesotaState == null)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ Creating required reference data (Minnesota state)...[/]");
            
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
            
            AnsiConsole.MarkupLine("[green]✅ Created Minnesota state record[/]");
            logger.LogInformation("Created Minnesota state record with ID 1");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✅ Minnesota state record exists[/]");
            logger.LogInformation("Minnesota state record already exists");
        }

        // Optionally create some default counties for Minnesota if needed
        var countyCount = await unitOfWork.Counties.CountAsync(c => c.StateId == 1);
        if (countyCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ Creating default Minnesota counties...[/]");
            
            var defaultCounties = new[]
            {
                new County { Name = "Cook", StateId = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new County { Name = "Lake", StateId = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new County { Name = "St. Louis", StateId = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new County { Name = "Unknown", StateId = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
            };

            foreach (var county in defaultCounties)
            {
                await unitOfWork.Counties.AddAsync(county);
            }
            
            await unitOfWork.SaveChangesAsync();
            
            AnsiConsole.MarkupLine($"[green]✅ Created {defaultCounties.Length} default counties[/]");
            logger.LogInformation("Created {CountyCount} default counties for Minnesota", defaultCounties.Length);
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✅ Found {countyCount} existing counties for Minnesota[/]");
            logger.LogInformation("Found {CountyCount} existing counties for Minnesota", countyCount);
        }
    }
}
