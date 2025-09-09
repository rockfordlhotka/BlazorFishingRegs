using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Diagnostic program to analyze why water body extraction is not finding all entries
/// </summary>
class WaterBodyExtractionDiagnostic
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunDiagnostic(string[] args)
    {
        AnsiConsole.Write(
            new Panel(new Text("Water Body Extraction Diagnostic", style: "bold"))
                .BorderColor(Color.Yellow)
                .Header("[red]DEBUG MODE[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[yellow]This will analyze the extraction process step by step[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Setup services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URI");
            services.AddTextProcessingServicesWithSecureConfig(UserSecretsId, keyVaultUri);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<WaterBodyExtractionDiagnostic>>();
            var extractionService = serviceProvider.GetRequiredService<IComprehensiveDataExtractionService>();

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]? Test file not found:[/] {testTextPath}");
                return;
            }

            var textContent = await File.ReadAllTextAsync(testTextPath);
            AnsiConsole.MarkupLine($"[green]? Loaded document:[/] {textContent.Length:N0} characters");

            // Step 1: Analyze the document structure
            AnsiConsole.Write(new Rule("[blue]Step 1: Document Analysis[/]"));
            
            // Look for the special regulations section manually - both TOC and actual content
            var tocStart = textContent.IndexOf("Waters With Experimental and Special Regulations", StringComparison.OrdinalIgnoreCase);
            AnsiConsole.MarkupLine($"Table of Contents entry found at position: {tocStart}");
            
            // Look for first actual lake entry
            var lakePattern = @"[A-Z]+ LAKE \([A-Za-z\s]+\)";
            var firstLakeMatch = System.Text.RegularExpressions.Regex.Match(textContent, lakePattern);
            
            if (firstLakeMatch.Success)
            {
                AnsiConsole.MarkupLine($"First actual lake entry found at position: {firstLakeMatch.Index}");
                AnsiConsole.MarkupLine($"First lake: [green]{firstLakeMatch.Value}[/]");
                
                // Show distance between TOC and actual content
                if (tocStart >= 0)
                {
                    var distance = firstLakeMatch.Index - tocStart;
                    AnsiConsole.MarkupLine($"Distance from TOC to content: {distance:N0} characters");
                }
                
                // Extract a reasonable section around the first lake for analysis
                var contentStart = Math.Max(0, firstLakeMatch.Index - 500);
                var contentLength = Math.Min(2000, textContent.Length - contentStart);
                var actualContent = textContent.Substring(contentStart, contentLength);
                
                AnsiConsole.MarkupLine($"[cyan]Sample actual content around first lake:[/]");
                var contentSample = actualContent.Substring(0, Math.Min(800, actualContent.Length));
                AnsiConsole.MarkupLine($"[dim]{contentSample.Replace("\n", "\\n").Replace("\r", "")}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]No actual lake entries found with standard pattern![/]");
            }

            // Step 2: Count potential water bodies manually using regex
            AnsiConsole.Write(new Rule("[blue]Step 2: Manual Pattern Matching[/]"));
            
            lakePattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([^)]+)\)";
            var matches = System.Text.RegularExpressions.Regex.Matches(textContent, lakePattern);
            
            AnsiConsole.MarkupLine($"Regex found {matches.Count} potential water body patterns");
            
            // Show first 10 matches
            AnsiConsole.MarkupLine("[cyan]First 10 matches:[/]");
            for (int i = 0; i < Math.Min(10, matches.Count); i++)
            {
                var match = matches[i];
                var name = match.Groups[1].Value.Trim();
                var county = match.Groups[2].Value.Trim();
                AnsiConsole.MarkupLine($"  {i + 1}. [green]{name}[/] ([blue]{county}[/])");
            }

            // Step 3: Test AI extraction
            AnsiConsole.Write(new Rule("[blue]Step 3: AI Extraction Test[/]"));
            
            var result = await extractionService.ExtractAllWaterBodiesAsync(textContent);
            
            if (result.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[green]? AI extracted {result.Data?.Count ?? 0} water bodies[/]");
                
                if (result.Data != null && result.Data.Count > 0)
                {
                    AnsiConsole.MarkupLine("[cyan]First 10 AI extracted water bodies:[/]");
                    for (int i = 0; i < Math.Min(10, result.Data.Count); i++)
                    {
                        var wb = result.Data[i];
                        AnsiConsole.MarkupLine($"  {i + 1}. [green]{wb.Name}[/] ([blue]{wb.County}[/]) - {wb.WaterType}");
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]? AI extraction failed:[/] {result.ErrorMessage}");
            }

            // Step 4: Test with the corrected extraction method
            AnsiConsole.Write(new Rule("[blue]Step 4: Test New Extraction Method[/]"));
            
            // Test the new extraction logic manually
            var lakePatterns = new[]
            {
                @"[A-Z]+ LAKE \([A-Za-z\s]+\)",
                @"[A-Z][A-Z\s]+ LAKE \([A-Za-z\s]+\)"
            };

            int actualContentStart = -1;
            foreach (var pattern in lakePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(textContent, pattern);
                if (match.Success)
                {
                    actualContentStart = match.Index;
                    break;
                }
            }

            if (actualContentStart >= 0)
            {
                // Find end point
                var endPatterns = new[] { "BORDER WATERS", "BOWFISHING", "DARK HOUSE SPEARING", "ICE ANGLING", "ILLUSTRATED FISH" };
                int endIndex = textContent.Length;
                foreach (var pattern in endPatterns)
                {
                    var foundEndIndex = textContent.IndexOf(pattern, actualContentStart + 1000, StringComparison.OrdinalIgnoreCase);
                    if (foundEndIndex >= 0 && foundEndIndex < endIndex)
                    {
                        endIndex = foundEndIndex;
                    }
                }

                var correctedSection = textContent.Substring(actualContentStart, endIndex - actualContentStart);
                AnsiConsole.MarkupLine($"[green]? Corrected section extraction:[/] {correctedSection.Length:N0} characters");
                
                // Count lakes in corrected section
                var correctedMatches = System.Text.RegularExpressions.Regex.Matches(correctedSection, lakePattern);
                AnsiConsole.MarkupLine($"[green]Lakes found in corrected section: {correctedMatches.Count}[/]");
                
                // Show first few lakes from corrected section
                AnsiConsole.MarkupLine("[cyan]First 5 lakes from corrected section:[/]");
                for (int i = 0; i < Math.Min(5, correctedMatches.Count); i++)
                {
                    AnsiConsole.MarkupLine($"  {i + 1}. [green]{correctedMatches[i].Value}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]? Could not find actual content start[/]");
            }

            // Step 5: Recommendations
            AnsiConsole.Write(new Rule("[blue]Step 5: Analysis & Recommendations[/]"));
            
            var regexCount = matches.Count;
            var aiCount = result.Data?.Count ?? 0;
            
            if (regexCount > aiCount * 5) // If regex finds significantly more
            {
                AnsiConsole.MarkupLine("[yellow]?? Issue: AI is missing many water bodies that regex can find[/]");
                AnsiConsole.MarkupLine("[cyan]Recommendations:[/]");
                AnsiConsole.MarkupLine("• AI prompts may need better examples");
                AnsiConsole.MarkupLine("• Chunk size might be too small");
                AnsiConsole.MarkupLine("• AI may be filtering out valid entries");
                AnsiConsole.MarkupLine("• Token limits may be cutting off responses");
            }
            else if (aiCount < 50)
            {
                AnsiConsole.MarkupLine("[yellow]?? Issue: Both regex and AI finding relatively few entries[/]");
                AnsiConsole.MarkupLine("[cyan]Recommendations:[/]");
                AnsiConsole.MarkupLine("• Document structure may be different than expected");
                AnsiConsole.MarkupLine("• Special regulations section may not contain all water bodies");
                AnsiConsole.MarkupLine("• Need to check other sections of the document");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]? Extraction appears to be working as expected[/]");
            }

        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Diagnostic failed:[/] {ex.Message}");
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
//    private static IConfiguration BuildConfiguration()
//    {
//        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
//        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
//
//        var builder = new ConfigurationBuilder()
//            .SetBasePath(Directory.GetCurrentDirectory())
//            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
//            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
//
//        if (isDevelopment)
//        {
//            builder.AddUserSecrets(UserSecretsId);
//        }
//
//        builder.AddEnvironmentVariables();
//
//        return builder.Build();
//    }
//}
//    private static IConfiguration BuildConfiguration()
//    {
//        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
//        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
//
//        var builder = new ConfigurationBuilder()
//            .SetBasePath(Directory.GetCurrentDirectory())
//            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
//            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
//
//        if (isDevelopment)
//        {
//            builder.AddUserSecrets(UserSecretsId);
//        }
//
//        builder.AddEnvironmentVariables();
//
//        return builder.Build();
//    }
//}