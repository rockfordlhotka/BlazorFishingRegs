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

    static async Task Main(string[] args)
    {
        Console.WriteLine("Fishing Regulations PDF Processing Test");
        Console.WriteLine("======================================\n");

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

            // Register PDF processing services with secure configuration
            services.AddPdfProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Starting PDF processing test with secure configuration...");

            // Get the PDF processing service
            var pdfProcessingService = serviceProvider.GetRequiredService<IPdfProcessingService>();

            // Test with the fishing regulations PDF
            var testPdfPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.pdf";
            
            if (!File.Exists(testPdfPath))
            {
                logger.LogError("Test PDF file not found at: {FilePath}", testPdfPath);
                Console.WriteLine($"Test PDF file not found at: {testPdfPath}");
                Console.WriteLine("Please ensure the fishing_regs.pdf file exists in the data folder.");
                return;
            }

            logger.LogInformation("Found test PDF at: {FilePath}", testPdfPath);
            Console.WriteLine($"Processing PDF: {testPdfPath}\n");

            // Read the PDF file
            using var fileStream = File.OpenRead(testPdfPath);
            var fileName = Path.GetFileName(testPdfPath);
            const string contentType = "application/pdf";

            // Validate the PDF
            Console.WriteLine("1. Validating PDF file...");
            fileStream.Position = 0;
            var isValid = await pdfProcessingService.ValidatePdfAsync(fileName, contentType, fileStream.Length, fileStream);
            
            if (!isValid)
            {
                Console.WriteLine("❌ PDF validation failed!");
                return;
            }
            
            Console.WriteLine("✅ PDF validation passed!");

            // Process the PDF
            Console.WriteLine("\n2. Processing PDF with Azure Document Intelligence...");
            fileStream.Position = 0;
            
            var processingResult = await pdfProcessingService.ProcessPdfAsync(
                fileStream, fileName, contentType);

            Console.WriteLine($"Processing Status: {processingResult.Status}");
            
            if (processingResult.Status == FishingRegs.Services.Models.DocumentProcessingStatus.Failed)
            {
                Console.WriteLine($"❌ Processing failed: {processingResult.ErrorMessage}");
                return;
            }

            if (processingResult.Status == FishingRegs.Services.Models.DocumentProcessingStatus.Completed)
            {
                Console.WriteLine("✅ PDF processing completed successfully!");
                
                if (processingResult.AnalysisResult != null)
                {
                    Console.WriteLine($"\nAnalysis Results:");
                    Console.WriteLine($"  Document Type: {processingResult.AnalysisResult.DocumentType}");
                    Console.WriteLine($"  Extracted Fields: {processingResult.AnalysisResult.ExtractedFields.Count}");
                    Console.WriteLine($"  Tables Found: {processingResult.AnalysisResult.Tables.Count}");
                    Console.WriteLine($"  Overall Confidence: {processingResult.AnalysisResult.ConfidenceScores.GetValueOrDefault("OverallConfidence", 0):P2}");

                    // Show some extracted fields
                    Console.WriteLine("\n  Sample Extracted Fields:");
                    foreach (var field in processingResult.AnalysisResult.ExtractedFields.Take(5))
                    {
                        Console.WriteLine($"    {field.Key}: {field.Value.Value.Substring(0, Math.Min(50, field.Value.Value.Length))}...");
                    }

                    // Try to extract fishing regulation data
                    Console.WriteLine("\n3. Extracting fishing regulation data...");
                    try
                    {
                        var regulationData = await pdfProcessingService.ExtractFishingRegulationDataAsync(processingResult.AnalysisResult);
                        Console.WriteLine($"✅ Found regulations for {regulationData.Lakes.Count} lakes");
                        
                        // Show first few lakes
                        foreach (var lake in regulationData.Lakes.Take(3))
                        {
                            Console.WriteLine($"  - {lake.LakeName} ({lake.Species.Count} species regulations)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error extracting regulation data: {ex.Message}");
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
            Console.WriteLine($"  dotnet user-secrets set \"AzureAI:DocumentIntelligence:Endpoint\" \"https://your-doc-intelligence.cognitiveservices.azure.com/\"");
            Console.WriteLine($"  dotnet user-secrets set \"AzureAI:DocumentIntelligence:ApiKey\" \"your-api-key\"");
            Console.WriteLine($"  dotnet user-secrets set \"ConnectionStrings:AzureStorage\" \"your-storage-connection-string\"");
            Console.WriteLine("\nFor PRODUCTION (Azure Key Vault):");
            Console.WriteLine("  1. Create an Azure Key Vault");
            Console.WriteLine("  2. Add secrets:");
            Console.WriteLine("     - AzureAI--DocumentIntelligence--Endpoint");
            Console.WriteLine("     - AzureAI--DocumentIntelligence--ApiKey");
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
