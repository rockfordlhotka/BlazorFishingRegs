using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;

namespace FishingRegs.TestConsole;

class Program
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    static async Task MainOriginal(string[] args)
    {
        Console.WriteLine("Fishing Regulations Text Processing Test");
        Console.WriteLine("=======================================\n");

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

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Starting text processing test with secure configuration...");

            // Get the text processing service
            var textProcessingService = serviceProvider.GetRequiredService<ITextProcessingService>();

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

            // Read the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);
            var fileName = Path.GetFileName(testTextPath);

            // Validate the text content
            Console.WriteLine("1. Validating text content...");
            var isValid = await textProcessingService.ValidateTextAsync(fileName, textContent);
            
            if (!isValid)
            {
                Console.WriteLine("❌ Text validation failed!");
                return;
            }
            
            Console.WriteLine("✅ Text validation passed!");

            // Process the text
            Console.WriteLine("\n2. Processing text with AI extraction...");
            
            var processingResult = await textProcessingService.ProcessTextAsync(
                textContent, fileName);

            Console.WriteLine($"Processing Status: {processingResult.Status}");
            
            if (processingResult.Status == FishingRegs.Services.Models.DocumentProcessingStatus.Failed)
            {
                Console.WriteLine($"❌ Processing failed: {processingResult.ErrorMessage}");
                return;
            }

            if (processingResult.Status == FishingRegs.Services.Models.DocumentProcessingStatus.Completed)
            {
                Console.WriteLine("✅ Text processing completed successfully!");
                
                if (processingResult.FishingRegulationData != null)
                {
                    var data = processingResult.FishingRegulationData;
                    Console.WriteLine($"\nExtraction Results:");
                    Console.WriteLine($"  Document: {data.DocumentName}");
                    Console.WriteLine($"  Lakes Processed: {data.TotalLakesProcessed}");
                    Console.WriteLine($"  Regulations Extracted: {data.TotalRegulationsExtracted}");
                    Console.WriteLine($"  Overall Confidence: {data.OverallConfidence:P2}");

                    // Show some extracted lake regulations
                    Console.WriteLine("\n  Sample Lake Regulations:");
                    foreach (var lake in data.LakeRegulations.Take(5))
                    {
                        Console.WriteLine($"    {lake.LakeName} ({lake.County}): {lake.Species.Count} species regulations");
                    }

                    // Try to extract detailed fishing regulation data
                    Console.WriteLine("\n3. Extracting detailed fishing regulation data...");
                    try
                    {
                        var detailedData = await textProcessingService.ExtractFishingRegulationDataAsync(textContent, fileName);
                        Console.WriteLine($"✅ Found regulations for {detailedData.LakeRegulations.Count} lakes");
                        
                        // Show first few lakes with more detail
                        foreach (var lake in detailedData.LakeRegulations.Take(3))
                        {
                            Console.WriteLine($"  - {lake.LakeName} ({lake.Species.Count} species regulations)");
                            foreach (var species in lake.Species.Take(2))
                            {
                                Console.WriteLine($"    * {species.SpeciesName}: Daily limit {species.DailyLimit ?? 0}, Min size {species.MinimumSizeInches ?? 0}\"");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error extracting detailed regulation data: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine("\n✅ Test completed successfully!");
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
            Console.WriteLine($"  dotnet user-secrets set \"ConnectionStrings:AzureStorage\" \"your-storage-connection-string\"");
            Console.WriteLine("\nFor PRODUCTION (Azure Key Vault):");
            Console.WriteLine("  1. Create an Azure Key Vault");
            Console.WriteLine("  2. Add secrets:");
            Console.WriteLine("     - AzureAI--OpenAI--Endpoint");
            Console.WriteLine("     - AzureAI--OpenAI--ApiKey");
            Console.WriteLine("     - AzureAI--OpenAI--DeploymentName");
            Console.WriteLine("     - ConnectionStrings--AzureStorage");
            Console.WriteLine("  3. Set environment variable: AZURE_KEY_VAULT_URI");
            Console.WriteLine("  4. Ensure your app has permission to access the Key Vault");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
