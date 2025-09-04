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

namespace FishingRegs.TestConsole;

/// <summary>
/// Complete test program for Section 3.2: Text upload -> AI extraction -> Database population
/// </summary>
class DatabasePopulationTestProgram
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    static async Task MainDatabase(string[] args)
    {
        Console.WriteLine("Section 3.2 - Fishing Regulations Database Population Test");
        Console.WriteLine("========================================================\n");

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
            Console.WriteLine("\n  Sample fish species:");
            foreach (var species in sampleSpecies.Take(5))
            {
                var regulationCount = await unitOfWork.FishingRegulations.CountAsync(fr => 
                    fr.SpeciesId == species.Id && fr.IsActive);
                Console.WriteLine($"    {species.CommonName} (ID: {species.Id}): {regulationCount} regulations");
            }

            // Step 4: Test specific queries
            Console.WriteLine("\n4. Testing regulation queries...");

            if (totalWaterBodies > 0 && totalFishSpecies > 0)
            {
                var firstWaterBody = sampleWaterBodies.First();
                var regulations = await unitOfWork.FishingRegulations.GetByWaterBodyAsync(firstWaterBody.Id);
                
                Console.WriteLine($"✅ Query test for {firstWaterBody.Name}:");
                Console.WriteLine($"  - Found {regulations.Count()} regulations");
                
                foreach (var regulation in regulations.Take(3))
                {
                    Console.WriteLine($"    - Species ID {regulation.SpeciesId}: Daily limit {regulation.DailyLimit}, " +
                                    $"Min size {regulation.MinimumSizeInches}\"");
                }
            }

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
            Console.WriteLine($"  dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"your-database-connection-string\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
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
