using FishingRegs.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace FishingRegs.TestConsole;

/// <summary>
/// Test to debug parsing issues with specific lakes like "Little Rabbit Lake"
/// </summary>
public static class LakeParsingTest
{
    public static async Task RunParsingTest(string[] args)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]Lake Parsing Debug Test[/]");
            AnsiConsole.WriteLine();

            // Setup minimal services
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();
                
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<AiLakeRegulationExtractionService>();

            var extractionService = new AiLakeRegulationExtractionService(logger, configuration);

            // Read the fishing regulations text
            var textPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            if (!File.Exists(textPath))
            {
                AnsiConsole.MarkupLine($"[red]Text file not found: {textPath}[/]");
                return;
            }

            var regulationsText = await File.ReadAllTextAsync(textPath);
            AnsiConsole.MarkupLine($"[green]Loaded regulations text: {regulationsText.Length:N0} characters[/]");

            // Extract the special regulations section
            var specialRegulationsSection = GetSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                AnsiConsole.MarkupLine("[red]Could not find special regulations section[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Extracted special regulations section: {specialRegulationsSection.Length:N0} characters[/]");

            // Parse lake entries
            var lakeEntries = extractionService.ParseLakeEntries(specialRegulationsSection);
            AnsiConsole.MarkupLine($"[green]Found {lakeEntries.Count} lake entries[/]");

            // Look for Little Rabbit Lake specifically
            var littleRabbitLake = lakeEntries.FirstOrDefault(entry => 
                entry.LakeName.Contains("LITTLE RABBIT", StringComparison.OrdinalIgnoreCase));

            if (littleRabbitLake.LakeName != null)
            {
                AnsiConsole.MarkupLine($"[green]✅ Found Little Rabbit Lake:[/]");
                AnsiConsole.MarkupLine($"  [yellow]Name:[/] {littleRabbitLake.LakeName}");
                AnsiConsole.MarkupLine($"  [yellow]County:[/] {littleRabbitLake.County}");
                AnsiConsole.MarkupLine($"  [yellow]Regulation Text:[/] {littleRabbitLake.RegulationText}");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]❌ Little Rabbit Lake NOT found in parsed entries[/]");
            }

            // Look for DAM LAKE specifically (compound entry test)
            var damLake = lakeEntries.FirstOrDefault(entry => 
                entry.LakeName.Contains("DAM LAKE", StringComparison.OrdinalIgnoreCase));

            AnsiConsole.WriteLine();
            if (damLake.LakeName != null)
            {
                AnsiConsole.MarkupLine($"[green]✅ Found DAM LAKE (compound entry):[/]");
                AnsiConsole.MarkupLine($"  [yellow]Name:[/] {damLake.LakeName}");
                AnsiConsole.MarkupLine($"  [yellow]County:[/] {damLake.County}");
                AnsiConsole.MarkupLine($"  [yellow]Regulation Text:[/] {damLake.RegulationText}");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]❌ DAM LAKE NOT found in parsed entries[/]");
            }

            // Look for other cross-reference lakes that might be missing
            var crossRefLakes = lakeEntries.Where(entry => 
                entry.RegulationText.Contains("See ", StringComparison.OrdinalIgnoreCase)).ToList();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]Found {crossRefLakes.Count} lakes with cross-references:[/]");
            foreach (var lake in crossRefLakes.Take(10))
            {
                AnsiConsole.MarkupLine($"  • {lake.LakeName} ({lake.County}): {lake.RegulationText}");
            }

            // Look for compound lake entries (with "and connected", "including", etc.)
            var compoundLakes = lakeEntries.Where(entry => 
                entry.LakeName.Contains("and connected", StringComparison.OrdinalIgnoreCase) ||
                entry.LakeName.Contains("including", StringComparison.OrdinalIgnoreCase) ||
                entry.LakeName.Contains("and ", StringComparison.OrdinalIgnoreCase)).ToList();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]Found {compoundLakes.Count} compound lake entries:[/]");
            foreach (var lake in compoundLakes.Take(10))
            {
                AnsiConsole.MarkupLine($"  • {lake.LakeName} ({lake.County}): {lake.RegulationText.Substring(0, Math.Min(60, lake.RegulationText.Length))}...");
            }

            // Test the specific section around Little Rabbit Lake
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Testing specific section around Little Rabbit Lake:[/]");
            
            var littleRabbitIndex = regulationsText.IndexOf("LITTLE RABBIT LAKE", StringComparison.OrdinalIgnoreCase);
            if (littleRabbitIndex >= 0)
            {
                var start = Math.Max(0, littleRabbitIndex - 200);
                var length = Math.Min(400, regulationsText.Length - start);
                var contextSection = regulationsText.Substring(start, length);
                
                AnsiConsole.MarkupLine("[yellow]Context around Little Rabbit Lake:[/]");
                AnsiConsole.WriteLine(contextSection);
            }

        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private static string GetSpecialRegulationsSection(string regulationsText)
    {
        try
        {
            // Find the special regulations section header
            var startPattern = @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS";
            var startMatch = Regex.Match(regulationsText, startPattern, RegexOptions.IgnoreCase);
            
            if (!startMatch.Success)
            {
                // Try alternative patterns
                startMatch = Regex.Match(regulationsText, @"Special Regulations\s*Lakes \(County\)", RegexOptions.IgnoreCase);
                if (!startMatch.Success)
                {
                    return "";
                }
            }

            var startIndex = startMatch.Index + startMatch.Length;
            
            // Find end patterns
            var endPatterns = new[]
            {
                @"^\s*ILLUSTRATED FISH\s*$"
            };

            var endIndex = regulationsText.Length;
            foreach (var pattern in endPatterns)
            {
                var endMatch = Regex.Match(regulationsText.Substring(startIndex), pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (endMatch.Success)
                {
                    endIndex = Math.Min(endIndex, startIndex + endMatch.Index);
                }
            }

            return regulationsText.Substring(startIndex, endIndex - startIndex).Trim();
        }
        catch (Exception)
        {
            return "";
        }
    }
}