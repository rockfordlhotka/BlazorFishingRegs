using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Spectre.Console;
using System.Linq;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple regex-based water body extraction test - no AI, just pattern matching
/// </summary>
class RegexWaterBodyExtraction
{
    public static async Task RunRegexExtraction(string[] args)
    {
        AnsiConsole.Write(
            new Panel(new Text("Regex Water Body Extraction", style: "bold"))
                .BorderColor(Color.Green)
                .Header("[green]SIMPLE & RELIABLE[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[green]Extracting water bodies using regex pattern matching[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Load the fishing regulations file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]? Test file not found:[/] {testTextPath}");
                return;
            }

            var textContent = await File.ReadAllTextAsync(testTextPath);
            AnsiConsole.MarkupLine($"[green]? Loaded document:[/] {textContent.Length:N0} characters");

            // Extract water bodies using regex
            AnsiConsole.Write(new Rule("[blue]Regex Water Body Extraction[/]"));

            var waterBodies = ExtractWaterBodiesWithRegex(textContent);
            
            AnsiConsole.MarkupLine($"[green]? Found {waterBodies.Count} water bodies[/]");
            AnsiConsole.WriteLine();

            // Group by county for better display
            var groupedByCounty = waterBodies
                .GroupBy(wb => wb.County)
                .OrderBy(g => g.Key)
                .ToList();

            // Display results
            AnsiConsole.MarkupLine($"[cyan]Water bodies found in {groupedByCounty.Count} counties:[/]");
            AnsiConsole.WriteLine();

            foreach (var countyGroup in groupedByCounty.Take(10)) // Show first 10 counties
            {
                AnsiConsole.MarkupLine($"[yellow]{countyGroup.Key} County[/] ({countyGroup.Count()} water bodies):");
                
                foreach (var waterBody in countyGroup.Take(5)) // Show first 5 in each county
                {
                    var typeColor = waterBody.WaterType switch
                    {
                        "lake" => "blue",
                        "river" => "cyan",
                        "stream" => "green",
                        "pond" => "magenta",
                        "reservoir" => "yellow",
                        _ => "white"
                    };
                    
                    AnsiConsole.MarkupLine($"  • [{typeColor}]{waterBody.Name}[/] ({waterBody.WaterType})");
                }
                
                if (countyGroup.Count() > 5)
                {
                    AnsiConsole.MarkupLine($"  [dim]... and {countyGroup.Count() - 5} more[/]");
                }
                AnsiConsole.WriteLine();
            }

            if (groupedByCounty.Count > 10)
            {
                AnsiConsole.MarkupLine($"[dim]... and {groupedByCounty.Count - 10} more counties[/]");
            }

            // Summary statistics
            AnsiConsole.Write(new Rule("[blue]Summary Statistics[/]"));
            
            var statsTable = new Table()
                .AddColumn("Water Type")
                .AddColumn("Count")
                .AddColumn("Examples");

            var typeGroups = waterBodies
                .GroupBy(wb => wb.WaterType)
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var typeGroup in typeGroups)
            {
                var examples = string.Join(", ", typeGroup.Take(3).Select(wb => wb.Name));
                statsTable.AddRow(
                    typeGroup.Key.ToTitleCase(),
                    typeGroup.Count().ToString(),
                    examples
                );
            }
            
            AnsiConsole.Write(statsTable);

            // County statistics
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]Counties with most water bodies:[/]");
            
            var topCounties = groupedByCounty
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToList();

            foreach (var county in topCounties)
            {
                AnsiConsole.MarkupLine($"• [yellow]{county.Key}[/]: {county.Count()} water bodies");
            }

            // Export option
            AnsiConsole.WriteLine();
            if (AnsiConsole.Confirm("Export results to CSV file?"))
            {
                await ExportToCsv(waterBodies);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error:[/] {ex.Message}");
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    /// <summary>
    /// Extracts water bodies using regex patterns
    /// </summary>
    private static List<WaterBodyInfo> ExtractWaterBodiesWithRegex(string text)
    {
        var waterBodies = new List<WaterBodyInfo>();
        var seenCombinations = new HashSet<string>();

        // Primary pattern: "WATER BODY NAME (County)"
        var primaryPattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s\.]+)\)";
        var matches = Regex.Matches(text, primaryPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.Trim();
            var county = match.Groups[2].Value.Trim();

            // Clean and validate
            name = CleanWaterBodyName(name);
            county = CleanCountyName(county);

            if (IsValidWaterBodyEntry(name, county))
            {
                var waterType = DetermineWaterType(name);
                var key = $"{name}|{county}".ToLowerInvariant();

                if (!seenCombinations.Contains(key))
                {
                    waterBodies.Add(new WaterBodyInfo
                    {
                        Name = name,
                        County = county,
                        WaterType = waterType,
                        State = "Minnesota"
                    });
                    
                    seenCombinations.Add(key);
                }
            }
        }

        return waterBodies.OrderBy(wb => wb.County).ThenBy(wb => wb.Name).ToList();
    }

    /// <summary>
    /// Validates if the extracted entry is likely a real water body
    /// </summary>
    private static bool IsValidWaterBodyEntry(string name, string county)
    {
        // Basic validation rules
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(county))
            return false;

        if (name.Length < 3 || name.Length > 100)
            return false;

        if (county.Length < 3 || county.Length > 30)
            return false;

        // Filter out obvious non-water-body entries
        var excludePatterns = new[]
        {
            "SPECIES", "SEASON", "LIMIT", "ZONE", "OPEN", "CLOSED", 
            "POSSESSION", "SIZE", "DAILY", "WALLEYE", "PIKE", "BASS",
            "MUSKIE", "TROUT", "YEAR", "DNR", "ANGLING", "WATERS"
        };

        foreach (var pattern in excludePatterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // County validation - should look like a real county name
        var invalidCountyPatterns = new[] { "ONLY", "STREAMS", "RIVERS", "LAKES" };
        foreach (var pattern in invalidCountyPatterns)
        {
            if (county.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines water type from the name
    /// </summary>
    private static string DetermineWaterType(string name)
    {
        var nameLower = name.ToLowerInvariant();

        if (nameLower.Contains("river"))
            return "river";
        if (nameLower.Contains("stream") || nameLower.Contains("creek") || nameLower.Contains("brook"))
            return "stream";
        if (nameLower.Contains("pond"))
            return "pond";
        if (nameLower.Contains("reservoir"))
            return "reservoir";
        if (nameLower.Contains("chain") || nameLower.Contains("flowage"))
            return "chain";

        // Default to lake
        return "lake";
    }

    /// <summary>
    /// Cleans up water body names
    /// </summary>
    private static string CleanWaterBodyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove extra whitespace and periods
        name = Regex.Replace(name, @"\s+", " ").Trim();
        name = name.TrimEnd('.');

        // Convert to proper case
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                // Handle special cases
                if (words[i].Equals("ST", StringComparison.OrdinalIgnoreCase) || 
                    words[i].Equals("ST.", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = "St.";
                }
                else
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
                }
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Cleans up county names
    /// </summary>
    private static string CleanCountyName(string county)
    {
        if (string.IsNullOrWhiteSpace(county))
            return string.Empty;

        county = county.Trim();

        // Remove "County" suffix if present
        if (county.EndsWith("County", StringComparison.OrdinalIgnoreCase))
        {
            county = county[..^6].Trim();
        }

        // Remove extra periods and spaces
        county = Regex.Replace(county, @"\s+", " ").Trim();
        county = county.TrimEnd('.');

        // Convert to proper case
        var words = county.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                // Handle special cases like "St. Louis"
                if (words[i].Equals("ST", StringComparison.OrdinalIgnoreCase) || 
                    words[i].Equals("ST.", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = "St.";
                }
                else
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
                }
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Exports water bodies to CSV file
    /// </summary>
    private static async Task ExportToCsv(List<WaterBodyInfo> waterBodies)
    {
        try
        {
            var fileName = $"water_bodies_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Name,County,State,WaterType");

            foreach (var wb in waterBodies)
            {
                csv.AppendLine($"\"{wb.Name}\",\"{wb.County}\",\"{wb.State}\",\"{wb.WaterType}\"");
            }

            await File.WriteAllTextAsync(filePath, csv.ToString());
            AnsiConsole.MarkupLine($"[green]? Exported to:[/] {filePath}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Export failed:[/] {ex.Message}");
        }
    }
}

/// <summary>
/// Simple water body information structure
/// </summary>
public class WaterBodyInfo
{
    public string Name { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string WaterType { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for string formatting
/// </summary>
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return char.ToUpper(input[0]) + input[1..].ToLower();
    }
}