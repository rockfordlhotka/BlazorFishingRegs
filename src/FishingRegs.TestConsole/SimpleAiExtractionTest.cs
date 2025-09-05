using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple test for just the AI extraction part without database dependencies
/// </summary>
class SimpleAiExtractionTest
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunAiExtractionTest(string[] args)
    {
        Console.WriteLine("Simple AI Extraction Test");
        Console.WriteLine("========================\n");

        // Setup dependency injection with minimal configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        try
        {
            // Build configuration
            var configuration = BuildConfiguration();
            services.AddSingleton<IConfiguration>(configuration);

            // Check if we have the required Azure OpenAI configuration
            var endpoint = configuration["AzureAI:OpenAI:Endpoint"];
            var apiKey = configuration["AzureAI:OpenAI:ApiKey"];
            var deploymentName = configuration["AzureAI:OpenAI:DeploymentName"];

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deploymentName))
            {
                Console.WriteLine("❌ Missing Azure OpenAI configuration. Please set up user secrets:");
                Console.WriteLine("dotnet user-secrets set \"AzureAI:OpenAI:Endpoint\" \"https://your-openai.openai.azure.com/\"");
                Console.WriteLine("dotnet user-secrets set \"AzureAI:OpenAI:ApiKey\" \"your-api-key\"");
                Console.WriteLine("dotnet user-secrets set \"AzureAI:OpenAI:DeploymentName\" \"your-deployment-name\"");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"✅ Azure OpenAI Configuration found:");
            Console.WriteLine($"  - Endpoint: {endpoint}");
            Console.WriteLine($"  - Deployment: {deploymentName}");
            Console.WriteLine($"  - API Key: {(apiKey.Length > 8 ? apiKey.Substring(0, 8) + "..." : "***")}");
            Console.WriteLine();

            // Register only the AI extraction service
            services.AddScoped<IAiLakeRegulationExtractionService, FishingRegs.Services.Services.AiLakeRegulationExtractionService>();

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleAiExtractionTest>>();
            var aiExtractionService = serviceProvider.GetRequiredService<IAiLakeRegulationExtractionService>();

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                logger.LogError("Test text file not found at: {FilePath}", testTextPath);
                Console.WriteLine($"❌ Test text file not found at: {testTextPath}");
                return;
            }

            logger.LogInformation("Found test text file at: {FilePath}", testTextPath);
            Console.WriteLine($"📄 Processing text file: {testTextPath}\n");

            // Read the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);
            Console.WriteLine($"📊 File size: {textContent.Length:N0} characters");

            // Test the AI extraction
            Console.WriteLine("\n🤖 Starting AI extraction...");
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

            // Show detailed results
            Console.WriteLine($"\n📋 Extracted {extractionResult.ExtractedRegulations.Count} lake regulations:");
            
            foreach (var (lake, index) in extractionResult.ExtractedRegulations.Select((l, i) => (l, i)).Take(10))
            {
                Console.WriteLine($"\n  {index + 1}. {lake.LakeName} ({lake.County} County)");
                Console.WriteLine($"     Regulations: {lake.Regulations.SpecialRegulations.Count}");
                Console.WriteLine($"     Experimental: {lake.Regulations.IsExperimental}");
                
                foreach (var regulation in lake.Regulations.SpecialRegulations.Take(2))
                {
                    var details = new List<string>();
                    if (regulation.DailyLimit.HasValue) details.Add($"Daily limit: {regulation.DailyLimit}");
                    if (!string.IsNullOrEmpty(regulation.MinimumSize)) details.Add($"Min size: {regulation.MinimumSize}");
                    if (!string.IsNullOrEmpty(regulation.MaximumSize)) details.Add($"Max size: {regulation.MaximumSize}");
                    if (regulation.CatchAndRelease) details.Add("Catch & release");
                    
                    Console.WriteLine($"       • {regulation.Species}: {regulation.RegulationType} - {string.Join(", ", details)}");
                    if (!string.IsNullOrEmpty(regulation.Notes))
                    {
                        Console.WriteLine($"         Notes: {regulation.Notes.Substring(0, Math.Min(100, regulation.Notes.Length))}...");
                    }
                }
                
                if (lake.Regulations.SpecialRegulations.Count > 2)
                {
                    Console.WriteLine($"       ... and {lake.Regulations.SpecialRegulations.Count - 2} more regulations");
                }
            }

            if (extractionResult.ExtractedRegulations.Count > 10)
            {
                Console.WriteLine($"\n  ... and {extractionResult.ExtractedRegulations.Count - 10} more lakes");
            }

            Console.WriteLine("\n✅ AI extraction test completed successfully!");

            // Test individual lake extraction
            if (extractionResult.ExtractedRegulations.Any())
            {
                Console.WriteLine("\n🔍 Testing individual lake extraction...");
                var firstLake = extractionResult.ExtractedRegulations.First();
                var testText = $"{firstLake.LakeName} ({firstLake.County}) Sample regulation text for testing";
                
                var singleResult = await aiExtractionService.ExtractSingleLakeRegulationAsync(
                    testText, firstLake.LakeName, firstLake.County);
                
                if (singleResult != null)
                {
                    Console.WriteLine($"✅ Individual extraction successful for {singleResult.LakeName}");
                }
                else
                {
                    Console.WriteLine("❌ Individual extraction returned null");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        // Always add user secrets in Development
        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }
}
