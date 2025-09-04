using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Services;

namespace FishingRegs.TestConsole;

class SectionExtractionTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Section Extraction Debug Test");
        Console.WriteLine("============================\n");

        // Setup minimal logging
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<AiLakeRegulationExtractionService>>();

        // Setup minimal configuration (we won't actually call Azure OpenAI)
        var configData = new List<KeyValuePair<string, string?>>
        {
            new("AzureAI:OpenAI:Endpoint", "https://dummy.openai.azure.com/"),
            new("AzureAI:OpenAI:ApiKey", "dummy-key"),
            new("AzureAI:OpenAI:DeploymentName", "dummy-deployment")
        };
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
        
        if (!File.Exists(testTextPath))
        {
            Console.WriteLine($"Test text file not found at: {testTextPath}");
            return;
        }

        var textContent = await File.ReadAllTextAsync(testTextPath);
        Console.WriteLine($"Loaded text file: {textContent.Length} characters\n");

        // Create the service and use reflection to call the private method
        var service = new AiLakeRegulationExtractionService(logger, configuration);
        
        // Use reflection to access the private ExtractSpecialRegulationsSection method
        var method = typeof(AiLakeRegulationExtractionService).GetMethod("ExtractSpecialRegulationsSection", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method == null)
        {
            Console.WriteLine("Could not find ExtractSpecialRegulationsSection method");
            return;
        }

        Console.WriteLine("Calling ExtractSpecialRegulationsSection...\n");
        var sectionText = (string?)method.Invoke(service, new object[] { textContent }) ?? "";
        
        Console.WriteLine($"\nExtracted section length: {sectionText.Length} characters");
        
        if (!string.IsNullOrWhiteSpace(sectionText))
        {
            Console.WriteLine($"First 500 characters:\n{sectionText.Substring(0, Math.Min(500, sectionText.Length))}\n");
            
            // Test lake parsing
            Console.WriteLine("Testing lake parsing on extracted section...");
            var parseMethod = typeof(AiLakeRegulationExtractionService).GetMethod("ParseLakeEntries", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (parseMethod != null)
            {
                var result = parseMethod.Invoke(service, new object[] { sectionText });
                var lakeEntries = result as List<(string LakeName, string County, string RegulationText)> ?? new List<(string, string, string)>();
                Console.WriteLine($"Found {lakeEntries.Count} lake entries");
                
                for (int i = 0; i < Math.Min(3, lakeEntries.Count); i++)
                {
                    var entry = lakeEntries[i];
                    Console.WriteLine($"  Lake {i + 1}: {entry.LakeName} ({entry.County})");
                    Console.WriteLine($"    Regulation: {entry.RegulationText.Substring(0, Math.Min(100, entry.RegulationText.Length))}...");
                }
            }
        }
        else
        {
            Console.WriteLine("No section text extracted!");
        }

        Console.WriteLine("\nTest completed.");
    }
}
