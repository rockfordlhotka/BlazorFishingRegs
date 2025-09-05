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
using FishingRegs.Data.Repositories;
using FishingRegs.Data.Repositories.Implementation;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simplified test program for Section 3.2 using in-memory database
/// </summary>
class InMemoryDatabaseTestProgram
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    static async Task MainInMemory(string[] args)
    {
        Console.WriteLine("Section 3.2 - AI Extraction Test (In-Memory Database)");
        Console.WriteLine("====================================================\n");

        // Setup dependency injection with secure configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        try
        {
            // Get Key Vault URI from environment or arguments (optional for production)
            var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URI");
            
            Console.WriteLine($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}");
            Console.WriteLine($"Using Key Vault: {!string.IsNullOrWhiteSpace(keyVaultUri)}");
            Console.WriteLine($"Using User Secrets: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true}");
            Console.WriteLine();

            // Register text processing services with secure configuration
            services.AddTextProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            // Add in-memory database using the existing data access layer
            var configuration = BuildConfiguration();
            
            // Override the DbContext to use in-memory database
            services.AddDbContext<FishingRegsDbContext>(options =>
            {
                options.UseInMemoryDatabase("FishingRegsTestDB");
                options.EnableSensitiveDataLogging();
            });

            // Add the standard data access layer repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IWaterBodyRepository, WaterBodyRepository>();
            services.AddScoped<IFishingRegulationRepository, FishingRegulationRepository>();
            services.AddScoped<IFishSpeciesRepository, FishSpeciesRepository>();
            services.AddScoped<IStateRepository, StateRepository>();
            services.AddScoped<ICountyRepository, CountyRepository>();
            services.AddScoped<IRegulationDocumentRepository, RegulationDocumentRepository>();

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<InMemoryDatabaseTestProgram>>();
            
            logger.LogInformation("Starting Section 3.2 test with in-memory database...");

            // Initialize the database with seed data
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FishingRegsDbContext>();
                await SeedDatabaseAsync(context);
            }

            // Get services
            var aiExtractionService = serviceProvider.GetRequiredService<IAiLakeRegulationExtractionService>();
            var databasePopulationService = serviceProvider.GetRequiredService<IRegulationDatabasePopulationService>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                logger.LogError("Test text file not found at: {FilePath}", testTextPath);
                Console.WriteLine($"Test text file not found at: {testTextPath}");
                Console.WriteLine("Please ensure the fishing_regs.txt file exists in the data folder.");
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

            Console.WriteLine($"✅ Created source document record: {sourceDocument.Id}");

            // Read and process the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);

            // Step 1: AI Extraction
            Console.WriteLine("\n1. Extracting lake regulations using AI...");
            var extractionResult = await aiExtractionService.ExtractLakeRegulationsAsync(textContent);

            if (!extractionResult.IsSuccess)
            {
                Console.WriteLine($"❌ AI extraction failed: {extractionResult.ErrorMessage}");
                return;
            }

            Console.WriteLine($"✅ AI extraction completed successfully!");
            Console.WriteLine($"  - Total lakes processed: {extractionResult.TotalLakesProcessed}");
            Console.WriteLine($"  - Regulations extracted: {extractionResult.TotalRegulationsExtracted}");
            Console.WriteLine($"  - Processing time: {extractionResult.ProcessingTime.TotalSeconds:F2} seconds");

            if (extractionResult.ProcessingWarnings.Any())
            {
                Console.WriteLine($"  - Warnings: {extractionResult.ProcessingWarnings.Count}");
                foreach (var warning in extractionResult.ProcessingWarnings.Take(3))
                {
                    Console.WriteLine($"    ⚠️ {warning}");
                }
            }

            // Show sample extracted data
            Console.WriteLine("\n  Sample extracted regulations:");
            foreach (var lake in extractionResult.ExtractedRegulations.Take(3))
            {
                Console.WriteLine($"    {lake.LakeName} ({lake.County}): {lake.Regulations.SpecialRegulations.Count} special regulations");
                foreach (var regulation in lake.Regulations.SpecialRegulations.Take(2))
                {
                    Console.WriteLine($"      - {regulation.Species}: {regulation.RegulationType} ({regulation.Notes})");
                }
            }

            // Step 2: Database Population
            Console.WriteLine("\n2. Populating database with extracted regulations...");
            var populationResult = await databasePopulationService.PopulateDatabaseAsync(
                extractionResult, 
                sourceDocument.Id, 
                DateTime.Now.Year);

            if (!populationResult.IsSuccess)
            {
                Console.WriteLine($"❌ Database population failed: {populationResult.ErrorMessage}");
                
                if (populationResult.ProcessingErrors.Any())
                {
                    Console.WriteLine("  Processing errors:");
                    foreach (var error in populationResult.ProcessingErrors.Take(5))
                    {
                        Console.WriteLine($"    ❌ {error}");
                    }
                }
                return;
            }

            Console.WriteLine($"✅ Database population completed successfully!");
            Console.WriteLine($"  - Total lakes processed: {populationResult.TotalLakesProcessed}");
            Console.WriteLine($"  - Water bodies created: {populationResult.WaterBodiesCreated}");
            Console.WriteLine($"  - Water bodies updated: {populationResult.WaterBodiesUpdated}");
            Console.WriteLine($"  - Regulations created: {populationResult.RegulationsCreated}");
            Console.WriteLine($"  - Regulations updated: {populationResult.RegulationsUpdated}");
            Console.WriteLine($"  - Fish species created: {populationResult.FishSpeciesCreated}");
            Console.WriteLine($"  - Processing time: {populationResult.ProcessingTime.TotalSeconds:F2} seconds");

            if (populationResult.ProcessingWarnings.Any())
            {
                Console.WriteLine($"  - Warnings: {populationResult.ProcessingWarnings.Count}");
                foreach (var warning in populationResult.ProcessingWarnings.Take(3))
                {
                    Console.WriteLine($"    ⚠️ {warning}");
                }
            }

            // Step 3: Verify database contents
            Console.WriteLine("\n3. Verifying database contents...");

            var totalWaterBodies = await unitOfWork.WaterBodies.CountAsync(wb => wb.IsActive);
            var totalRegulations = await unitOfWork.FishingRegulations.CountAsync(fr => fr.IsActive);
            var totalFishSpecies = await unitOfWork.FishSpecies.CountAsync(fs => fs.IsActive);

            Console.WriteLine($"✅ Database verification:");
            Console.WriteLine($"  - Total active water bodies: {totalWaterBodies}");
            Console.WriteLine($"  - Total active fishing regulations: {totalRegulations}");
            Console.WriteLine($"  - Total active fish species: {totalFishSpecies}");

            Console.WriteLine("\n✅ Section 3.2 test completed successfully!");
            Console.WriteLine("\n🎉 Text upload -> AI extraction -> Database population pipeline is working!");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("configuration") || ex.Message.Contains("User Secrets") || ex.Message.Contains("Key Vault"))
        {
            Console.WriteLine($"❌ Configuration Error: {ex.Message}");
            Console.WriteLine("\n🔐 Secure Configuration Setup Required:");
            Console.WriteLine("\nFor DEVELOPMENT (User Secrets):");
            Console.WriteLine("  Run these commands in the FishingRegs.TestConsole directory:");
            Console.WriteLine($"  dotnet user-secrets set \"AzureAI:OpenAI:Endpoint\" \"https://your-openai.openai.azure.com/\"");
            Console.WriteLine($"  dotnet user-secrets set \"AzureAI:OpenAI:ApiKey\" \"your-api-key\"");
            Console.WriteLine($"  dotnet user-secrets set \"AzureAI:OpenAI:DeploymentName\" \"your-deployment-name\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nTest completed successfully!");
    }

    private static async Task SeedDatabaseAsync(FishingRegsDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed basic reference data if not exists
        if (!await context.States.AnyAsync())
        {
            var minnesota = new State
            {
                Id = 1,
                Name = "Minnesota",
                Code = "MN",
                Country = "US",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            context.States.Add(minnesota);
            await context.SaveChangesAsync();
        }

        // Seed some basic fish species if not exists
        if (!await context.FishSpecies.AnyAsync())
        {
            var species = new[]
            {
                new FishSpecies { Id = 1, CommonName = "Lake Trout", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new FishSpecies { Id = 2, CommonName = "Northern Pike", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new FishSpecies { Id = 3, CommonName = "Walleye", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new FishSpecies { Id = 4, CommonName = "Salmon", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new FishSpecies { Id = 5, CommonName = "Largemouth Bass", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
            };

            context.FishSpecies.AddRange(species);
            await context.SaveChangesAsync();
        }
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
