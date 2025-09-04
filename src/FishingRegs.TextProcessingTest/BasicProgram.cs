using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FishingRegs.TextProcessingTest;

/// <summary>
/// Console application to test basic text parsing of lake regulations without AI
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== FishingRegs Basic Text Parsing Test ===");
        Console.WriteLine();

        // Set up dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register basic parsing application
                services.AddSingleton<BasicParsingApplication>();
            })
            .Build();

        try
        {
            // Run the test application
            var app = host.Services.GetRequiredService<BasicParsingApplication>();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running test application: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}

/// <summary>
/// Basic parsing application to demonstrate text extraction
/// </summary>
public class BasicParsingApplication
{
    private readonly ILogger<BasicParsingApplication> _logger;

    public BasicParsingApplication(ILogger<BasicParsingApplication> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("1. Testing basic text parsing...");
        await TestBasicTextParsing();

        Console.WriteLine("\nTest completed successfully!");
    }

    private async Task TestBasicTextParsing()
    {
        try
        {
            // Test with the fishing regulations text file
            var textPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
            
            if (!File.Exists(textPath))
            {
                Console.WriteLine($"Text file not found: {textPath}");
                return;
            }

            Console.WriteLine($"Reading fishing regulations text from: {textPath}");
            
            // Read the text file
            var regulationsText = await File.ReadAllTextAsync(textPath);
            Console.WriteLine($"✓ Read {regulationsText.Length:N0} characters from text file");

            // Extract the special regulations section
            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                Console.WriteLine("✗ Could not find 'Waters With Experimental and Special Regulations' section");
                return;
            }

            Console.WriteLine($"✓ Extracted special regulations section: {specialRegulationsSection.Length:N0} characters");

            // Parse individual lake entries
            var lakeEntries = ParseLakeEntries(specialRegulationsSection);
            Console.WriteLine($"✓ Parsed {lakeEntries.Count} lake entries");

            // Show some examples
            Console.WriteLine("\nSample lake entries:");
            foreach (var (lakeName, county, regulationText) in lakeEntries.Take(10))
            {
                Console.WriteLine($"  {lakeName} ({county}):");
                var preview = regulationText.Length > 100 ? regulationText.Substring(0, 100) + "..." : regulationText;
                Console.WriteLine($"    {preview}");
                Console.WriteLine();
            }

            // Save basic parsing results
            await SaveBasicParsingResults(lakeEntries);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error in basic text parsing test: {ex.Message}");
            _logger.LogError(ex, "Error in basic text parsing test");
        }
    }

    private string ExtractSpecialRegulationsSection(string regulationsText)
    {
        try
        {
            // Find the start of the special regulations section
            var startPattern = @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS";
            var startMatch = Regex.Match(regulationsText, startPattern, RegexOptions.IgnoreCase);
            
            if (!startMatch.Success)
            {
                // Try alternative patterns
                startMatch = Regex.Match(regulationsText, @"Special Regulations\s*Lakes \(County\)", RegexOptions.IgnoreCase);
            }

            if (!startMatch.Success)
            {
                return "";
            }

            var startIndex = startMatch.Index;

            // Find the end of the section (next major section)
            var endPatterns = new[]
            {
                @"BORDER WATERS",
                @"BOWFISHING, SPEARING",
                @"DARK HOUSE SPEARING",
                @"ILLUSTRATED FISH"
            };

            var endIndex = regulationsText.Length;
            foreach (var pattern in endPatterns)
            {
                var endMatch = Regex.Match(regulationsText.Substring(startIndex), pattern, RegexOptions.IgnoreCase);
                if (endMatch.Success)
                {
                    endIndex = Math.Min(endIndex, startIndex + endMatch.Index);
                }
            }

            var sectionText = regulationsText.Substring(startIndex, endIndex - startIndex);
            
            // Clean up the text
            sectionText = Regex.Replace(sectionText, @"Page \d+.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
            sectionText = Regex.Replace(sectionText, @"\d+\s+2025 Minnesota Fishing Regulations.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
            
            return sectionText.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting special regulations section");
            return "";
        }
    }

    private List<(string LakeName, string County, string RegulationText)> ParseLakeEntries(string specialRegulationsSection)
    {
        var lakeEntries = new List<(string, string, string)>();

        try
        {
            // Use a simpler line-by-line approach
            var lines = specialRegulationsSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            string currentLake = "";
            string currentCounty = "";
            var currentRegulation = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip empty lines and headers
                if (string.IsNullOrWhiteSpace(trimmedLine) || 
                    trimmedLine.Contains("Special Regulations") ||
                    trimmedLine.Contains("National Wildlife") ||
                    trimmedLine.Contains("Voyageurs"))
                    continue;
                
                // Check if this line looks like a lake header
                var lakeMatch = Regex.Match(trimmedLine, @"^([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)\s*(.*)");
                if (lakeMatch.Success)
                {
                    // Save previous lake if we have one
                    if (!string.IsNullOrWhiteSpace(currentLake) && currentRegulation.Count > 0)
                    {
                        lakeEntries.Add((currentLake, currentCounty, string.Join(" ", currentRegulation)));
                    }

                    // Start new lake
                    currentLake = Regex.Replace(lakeMatch.Groups[1].Value.Trim(), @"^[⁕NEW—]*", "");
                    currentCounty = lakeMatch.Groups[2].Value.Trim();
                    currentRegulation.Clear();
                    
                    if (!string.IsNullOrWhiteSpace(lakeMatch.Groups[3].Value))
                    {
                        currentRegulation.Add(lakeMatch.Groups[3].Value.Trim());
                    }
                }
                else if (!string.IsNullOrWhiteSpace(currentLake) && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // Add to current regulation if it doesn't look like a page number or header
                    if (!Regex.IsMatch(trimmedLine, @"^\d+\s*$") && 
                        !trimmedLine.Contains("2025 Minnesota Fishing Regulations"))
                    {
                        currentRegulation.Add(trimmedLine);
                    }
                }
            }

            // Add the last lake
            if (!string.IsNullOrWhiteSpace(currentLake) && currentRegulation.Count > 0)
            {
                lakeEntries.Add((currentLake, currentCounty, string.Join(" ", currentRegulation)));
            }

            _logger.LogInformation($"Parsed {lakeEntries.Count} lake entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lake entries");
        }

        return lakeEntries;
    }

    private async Task SaveBasicParsingResults(List<(string LakeName, string County, string RegulationText)> lakeEntries)
    {
        try
        {
            var outputDirectory = @"s:\src\rdl\BlazorAI-spec\data\basic-parsing-results";
            Directory.CreateDirectory(outputDirectory);

            Console.WriteLine($"\nSaving {lakeEntries.Count} basic parsing results...");

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Save all entries as a single JSON file
            var allEntries = lakeEntries.Select(entry => new
            {
                LakeName = entry.LakeName,
                County = entry.County,
                RegulationText = entry.RegulationText
            }).ToList();

            var allEntriesPath = Path.Combine(outputDirectory, "all-lake-entries.json");
            var allEntriesJson = JsonSerializer.Serialize(allEntries, jsonOptions);
            await File.WriteAllTextAsync(allEntriesPath, allEntriesJson);

            Console.WriteLine($"✓ Saved all lake entries to: {allEntriesPath}");

            // Create a summary file
            var summaryPath = Path.Combine(outputDirectory, "parsing-summary.json");
            var summary = new
            {
                ParseDate = DateTime.UtcNow,
                TotalLakes = lakeEntries.Count,
                Counties = lakeEntries.Select(e => e.County).Distinct().OrderBy(c => c).ToList(),
                SampleEntries = lakeEntries.Take(5).Select(e => new
                {
                    e.LakeName,
                    e.County,
                    RegulationPreview = e.RegulationText.Length > 100 ? e.RegulationText.Substring(0, 100) + "..." : e.RegulationText
                }).ToList()
            };

            var summaryJson = JsonSerializer.Serialize(summary, jsonOptions);
            await File.WriteAllTextAsync(summaryPath, summaryJson);
            
            Console.WriteLine($"✓ Saved parsing summary to: {summaryPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error saving basic parsing results: {ex.Message}");
            _logger.LogError(ex, "Error saving basic parsing results");
        }
    }
}
