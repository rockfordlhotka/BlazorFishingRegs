using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Parser for the special-regulations.txt file to extract lake regulation data
/// </summary>
public class SpecialRegulationsParser
{
    public static async Task RunParser(string[] args)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("Special Regulations Parser")
                    .LeftJustified()
                    .Color(Color.Green));

            AnsiConsole.WriteLine();

            // Read the special-regulations.txt file
            var dataPath = @"S:\src\rdl\BlazorFishingRegs\data\special-regulations.txt";
            
            if (!File.Exists(dataPath))
            {
                AnsiConsole.MarkupLine($"[red]File not found: {dataPath}[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Reading file: {dataPath}[/]");
            var regulationsText = await File.ReadAllTextAsync(dataPath);
            AnsiConsole.MarkupLine($"[green]Loaded {regulationsText.Length:N0} characters[/]");

            // Extract the special regulations section
            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                AnsiConsole.MarkupLine("[red]Could not find special regulations section[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Extracted special regulations section: {specialRegulationsSection.Length:N0} characters[/]");

            // Parse lake entries
            var lakeEntries = ParseLakeEntries(specialRegulationsSection);
            AnsiConsole.MarkupLine($"[green]Found {lakeEntries.Count} lake entries[/]");

            // Display sample entries
            DisplaySampleEntries(lakeEntries);

            // Export to files
            await ExportToJson(lakeEntries);
            await ExportToCsv(lakeEntries);

            AnsiConsole.MarkupLine("\n[green]? Parsing complete![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private static string ExtractSpecialRegulationsSection(string regulationsText)
    {
        try
        {
            // Find the special regulations section header
            var startPatterns = new[]
            {
                @"Waters With Experimental and Special Regulations",
                @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS",
                @"Special Regulations"
            };

            int startIndex = -1;
            foreach (var pattern in startPatterns)
            {
                var match = Regex.Match(regulationsText, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    startIndex = match.Index + match.Length;
                    AnsiConsole.MarkupLine($"[cyan]Found section start using pattern: {pattern}[/]");
                    break;
                }
            }

            if (startIndex == -1)
            {
                return "";
            }

            // Find the end of the section
            var endPatterns = new[]
            {
                @"^\s*Border Waters",
                @"^\s*BORDER WATERS",
                @"^\s*Intensive Management",
                @"^\s*Page \d+"
            };

            var endIndex = regulationsText.Length;
            foreach (var pattern in endPatterns)
            {
                var endMatch = Regex.Match(regulationsText.Substring(startIndex), pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (endMatch.Success)
                {
                    var potentialEnd = startIndex + endMatch.Index;
                    if (potentialEnd < endIndex)
                    {
                        endIndex = potentialEnd;
                        AnsiConsole.MarkupLine($"[cyan]Found section end using pattern: {pattern}[/]");
                    }
                }
            }

            var sectionText = regulationsText.Substring(startIndex, endIndex - startIndex);
            
            // Clean up the text
            sectionText = CleanText(sectionText);
            
            return sectionText.Trim();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error extracting section: {ex.Message}[/]");
            return "";
        }
    }

    private static string CleanText(string text)
    {
        // Remove page numbers and headers/footers
        text = Regex.Replace(text, @"Page \d+.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\d+\s+20\d{2} Minnesota Fishing Regulations.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Skip to main content", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"eRegulations", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Minnesota\s*›\s*Fishing\s*›.*", "", RegexOptions.IgnoreCase);
        
        // Remove navigation and menu items
        text = Regex.Replace(text, @"Search MN Fishing", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"General Info", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Contact Information", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Health Advisory.*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Sunrise/Sunset.*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"State Record Fish.*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Additional Information", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Seasons & Limits", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Definititions", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Aquatic invasive Species", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Trespass Law", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Licenses, Permits & Fees", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Fishing Regulations", "", RegexOptions.IgnoreCase);
        
        return text;
    }

    private static List<LakeRegulationEntry> ParseLakeEntries(string specialRegulationsSection)
    {
        var lakeEntries = new List<LakeRegulationEntry>();

        try
        {
            // Normalize the text - keep line breaks for better parsing
            var normalizedText = specialRegulationsSection;
            
            // Split into lines for easier processing
            var lines = normalizedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            AnsiConsole.MarkupLine($"[cyan]Processing {lines.Count} lines of text[/]");

            string? currentLakeName = null;
            string? currentCounty = null;
            var currentRegulationParts = new List<string>();

            foreach (var line in lines)
            {
                // Skip section headers and navigation elements
                if (IsHeaderOrSection(line) || 
                    line.Length < 5 ||
                    Regex.IsMatch(line, @"^[A-Z]\s*$") || // Single letter section dividers
                    line.StartsWith("Skip to", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this line starts a new lake entry
                // Pattern: LAKE NAME (County) regulation text...
                var lakeMatch = Regex.Match(line, @"^([A-Z?][^()]+?)\s*\(([^)]+)\)\s*(.*)$");
                
                if (lakeMatch.Success)
                {
                    // Save previous lake entry if exists
                    if (currentLakeName != null && currentCounty != null && currentRegulationParts.Any())
                    {
                        var regulationText = string.Join(" ", currentRegulationParts).Trim();
                        if (!string.IsNullOrWhiteSpace(regulationText))
                        {
                            var entry = new LakeRegulationEntry
                            {
                                LakeName = currentLakeName,
                                County = currentCounty,
                                RegulationText = regulationText,
                                HasCrossReference = regulationText.Contains("See ", StringComparison.OrdinalIgnoreCase),
                                IsCompoundEntry = currentLakeName.Contains("and connected", StringComparison.OrdinalIgnoreCase) ||
                                                currentLakeName.Contains("including", StringComparison.OrdinalIgnoreCase),
                                SpeciesRegulations = ParseSpeciesRegulations(regulationText)
                            };
                            lakeEntries.Add(entry);
                        }
                    }

                    // Start new lake entry
                    currentLakeName = CleanLakeName(lakeMatch.Groups[1].Value);
                    currentCounty = lakeMatch.Groups[2].Value.Trim();
                    currentRegulationParts.Clear();
                    
                    var regulationStart = lakeMatch.Groups[3].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(regulationStart))
                    {
                        currentRegulationParts.Add(regulationStart);
                    }
                }
                else if (currentLakeName != null)
                {
                    // This is a continuation of the current lake's regulations
                    currentRegulationParts.Add(line);
                }
            }

            // Don't forget the last entry
            if (currentLakeName != null && currentCounty != null && currentRegulationParts.Any())
            {
                var regulationText = string.Join(" ", currentRegulationParts).Trim();
                if (!string.IsNullOrWhiteSpace(regulationText))
                {
                    var entry = new LakeRegulationEntry
                    {
                        LakeName = currentLakeName,
                        County = currentCounty,
                        RegulationText = regulationText,
                        HasCrossReference = regulationText.Contains("See ", StringComparison.OrdinalIgnoreCase),
                        IsCompoundEntry = currentLakeName.Contains("and connected", StringComparison.OrdinalIgnoreCase) ||
                                        currentLakeName.Contains("including", StringComparison.OrdinalIgnoreCase),
                        SpeciesRegulations = ParseSpeciesRegulations(regulationText)
                    };
                    lakeEntries.Add(entry);
                }
            }

            AnsiConsole.MarkupLine($"[green]Successfully parsed {lakeEntries.Count} lake entries[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error parsing lake entries: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
        }

        return lakeEntries;
    }

    private static string CleanLakeName(string lakeName)
    {
        // Remove leading symbols
        lakeName = Regex.Replace(lakeName, @"^[?NEW—•\*\s]+", "").Trim();
        
        // Clean up extra whitespace
        lakeName = Regex.Replace(lakeName, @"\s+", " ").Trim();
        
        return lakeName;
    }

    private static List<SpeciesRegulation> ParseSpeciesRegulations(string regulationText)
    {
        var regulations = new List<SpeciesRegulation>();

        if (string.IsNullOrWhiteSpace(regulationText))
            return regulations;

        // Don't parse cross-references
        if (regulationText.Trim().StartsWith("See ", StringComparison.OrdinalIgnoreCase))
            return regulations;

        try
        {
            // Common fish species to look for
            var speciesPatterns = new[]
            {
                "walleye", "northern pike", "pike", "largemouth bass", "smallmouth bass", "bass",
                "muskie", "muskellunge", "trout", "lake trout", "salmon", "sunfish", "bluegill",
                "crappie", "perch", "yellow perch", "catfish", "tullibee", "sauger"
            };

            // Split regulation text by periods to get individual regulation statements
            var statements = regulationText.Split('.', StringSplitOptions.RemoveEmptyEntries);

            foreach (var statement in statements)
            {
                var trimmedStatement = statement.Trim();
                if (string.IsNullOrWhiteSpace(trimmedStatement))
                    continue;

                // Check which species this statement refers to
                foreach (var species in speciesPatterns)
                {
                    // Case-insensitive check if the species is mentioned
                    if (trimmedStatement.Contains(species, StringComparison.OrdinalIgnoreCase))
                    {
                        var regulation = new SpeciesRegulation
                        {
                            Species = NormalizeSpeciesName(species),
                            RegulationDetails = trimmedStatement
                        };

                        // Parse specific regulation types
                        ParseRegulationDetails(regulation, trimmedStatement);

                        regulations.Add(regulation);
                        break; // Only match one species per statement
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silent fail - return what we have so far
        }

        return regulations;
    }

    private static void ParseRegulationDetails(SpeciesRegulation regulation, string text)
    {
        var lowerText = text.ToLower();

        // Check for catch-and-release
        if (lowerText.Contains("catch-and-release") || lowerText.Contains("catch?and?release"))
        {
            regulation.IsCatchAndRelease = true;
            regulation.RegulationType = "CatchAndRelease";
        }

        // Parse daily limit
        var dailyLimitMatch = Regex.Match(text, @"daily limit[:\s]+(\d+)", RegexOptions.IgnoreCase);
        if (dailyLimitMatch.Success && int.TryParse(dailyLimitMatch.Groups[1].Value, out var dailyLimit))
        {
            regulation.DailyLimit = dailyLimit;
            regulation.RegulationType = string.IsNullOrEmpty(regulation.RegulationType) ? "DailyLimit" : "Combined";
        }

        // Parse possession limit
        var possessionLimitMatch = Regex.Match(text, @"possession limit[:\s]+(\d+)", RegexOptions.IgnoreCase);
        if (possessionLimitMatch.Success && int.TryParse(possessionLimitMatch.Groups[1].Value, out var possessionLimit))
        {
            regulation.PossessionLimit = possessionLimit;
            regulation.RegulationType = string.IsNullOrEmpty(regulation.RegulationType) ? "PossessionLimit" : "Combined";
        }

        // Parse minimum size
        var minSizeMatch = Regex.Match(text, @"minimum size limit[:\s]+(\d+[""?]?)", RegexOptions.IgnoreCase);
        if (minSizeMatch.Success)
        {
            regulation.MinimumSize = minSizeMatch.Groups[1].Value;
            regulation.RegulationType = string.IsNullOrEmpty(regulation.RegulationType) ? "SizeLimit" : "Combined";
        }

        // Parse protected slot (e.g., "all from 24-36" must be released)
        var protectedSlotMatch = Regex.Match(text, @"all from (\d+[-?]\d+)[""?]?\s+must be", RegexOptions.IgnoreCase);
        if (protectedSlotMatch.Success)
        {
            regulation.ProtectedSlot = protectedSlotMatch.Groups[1].Value + "\"";
            regulation.RegulationType = string.IsNullOrEmpty(regulation.RegulationType) ? "ProtectedSlot" : "Combined";
        }

        // Parse size restrictions in format "all X-Y must be released"
        var sizeRangeMatch = Regex.Match(text, @"all (?:from )?(\d+)[-?](\d+)[""?]?", RegexOptions.IgnoreCase);
        if (sizeRangeMatch.Success && string.IsNullOrEmpty(regulation.ProtectedSlot))
        {
            regulation.ProtectedSlot = $"{sizeRangeMatch.Groups[1].Value}-{sizeRangeMatch.Groups[2].Value}\"";
            regulation.RegulationType = string.IsNullOrEmpty(regulation.RegulationType) ? "ProtectedSlot" : "Combined";
        }

        // Default if no specific type was determined
        if (string.IsNullOrEmpty(regulation.RegulationType))
        {
            regulation.RegulationType = "General";
        }
    }

    private static string NormalizeSpeciesName(string species)
    {
        // Convert to title case and handle special cases
        var normalized = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(species.ToLower());
        
        // Handle specific replacements
        normalized = normalized switch
        {
            "Pike" => "Northern Pike",
            _ => normalized
        };

        return normalized;
    }

    private static bool IsHeaderOrSection(string text)
    {
        var headerPatterns = new[]
        {
            "National Wildlife",
            "Voyageurs",
            "Intensive Management",
            "Special Regulations",
            "Experimental",
            "Border Waters",
            "Minnesota",
            "Fishing",
            "Search MN",
            "General Info",
            "Seasons",
            "Limits"
        };

        // Check if it's a single letter section divider (A, B, C, etc.)
        if (Regex.IsMatch(text, @"^[A-Z]\s*$"))
        {
            return true;
        }

        return headerPatterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static void DisplaySampleEntries(List<LakeRegulationEntry> entries)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Sample Entries (first 10):[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Lake Name");
        table.AddColumn("County");
        table.AddColumn("Species Count");
        table.AddColumn("Regulation Preview");

        foreach (var entry in entries.Take(10))
        {
            var preview = entry.RegulationText.Length > 60 
                ? entry.RegulationText.Substring(0, 60) + "..." 
                : entry.RegulationText;

            table.AddRow(
                entry.LakeName,
                entry.County,
                entry.SpeciesRegulations.Count.ToString(),
                preview
            );
        }

        AnsiConsole.Write(table);

        // Display some detailed species examples
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Sample Species Regulations (from first entry with species data):[/]");
        
        var entryWithSpecies = entries.FirstOrDefault(e => e.SpeciesRegulations.Any());
        if (entryWithSpecies != null)
        {
            AnsiConsole.MarkupLine($"[yellow]Lake: {entryWithSpecies.LakeName} ({entryWithSpecies.County})[/]");
            
            var speciesTable = new Table();
            speciesTable.Border(TableBorder.Rounded);
            speciesTable.AddColumn("Species");
            speciesTable.AddColumn("Type");
            speciesTable.AddColumn("Daily");
            speciesTable.AddColumn("Possession");
            speciesTable.AddColumn("Details");

            foreach (var species in entryWithSpecies.SpeciesRegulations.Take(5))
            {
                speciesTable.AddRow(
                    species.Species,
                    species.RegulationType,
                    species.DailyLimit?.ToString() ?? "-",
                    species.PossessionLimit?.ToString() ?? "-",
                    species.RegulationDetails.Length > 40 
                        ? species.RegulationDetails.Substring(0, 40) + "..." 
                        : species.RegulationDetails
                );
            }

            AnsiConsole.Write(speciesTable);
        }

        // Display statistics
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Statistics:[/]");
        AnsiConsole.MarkupLine($"  Total entries: {entries.Count}");
        AnsiConsole.MarkupLine($"  Cross-references: {entries.Count(e => e.HasCrossReference)}");
        AnsiConsole.MarkupLine($"  Compound entries: {entries.Count(e => e.IsCompoundEntry)}");
        AnsiConsole.MarkupLine($"  Entries with species data: {entries.Count(e => e.SpeciesRegulations.Any())}");
        AnsiConsole.MarkupLine($"  Total species regulations: {entries.Sum(e => e.SpeciesRegulations.Count)}");
        AnsiConsole.MarkupLine($"  Unique counties: {entries.Select(e => e.County).Distinct().Count()}");
        
        var speciesCounts = entries
            .SelectMany(e => e.SpeciesRegulations)
            .GroupBy(s => s.Species)
            .OrderByDescending(g => g.Count())
            .Take(10);
            
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Top 10 Most Regulated Species:[/]");
        foreach (var group in speciesCounts)
        {
            AnsiConsole.MarkupLine($"  {group.Key}: {group.Count()} regulations");
        }
    }

    private static async Task ExportToJson(List<LakeRegulationEntry> entries)
    {
        try
        {
            var outputDir = @"S:\src\rdl\BlazorFishingRegs\data\parsed-output";
            Directory.CreateDirectory(outputDir);

            var outputPath = Path.Combine(outputDir, "special-regulations-parsed.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(entries, options);
            await File.WriteAllTextAsync(outputPath, json);

            AnsiConsole.MarkupLine($"\n[green]? Exported to JSON: {outputPath}[/]");
            AnsiConsole.MarkupLine($"  File size: {new FileInfo(outputPath).Length:N0} bytes");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error exporting to JSON: {ex.Message}[/]");
        }
    }

    private static async Task ExportToCsv(List<LakeRegulationEntry> entries)
    {
        try
        {
            var outputDir = @"S:\src\rdl\BlazorFishingRegs\data\parsed-output";
            Directory.CreateDirectory(outputDir);

            // Export lake entries
            var lakeOutputPath = Path.Combine(outputDir, "special-regulations-lakes.csv");
            var lakeCsv = new StringBuilder();
            lakeCsv.AppendLine("LakeName,County,RegulationText,HasCrossReference,IsCompoundEntry,SpeciesCount");

            foreach (var entry in entries)
            {
                lakeCsv.AppendLine($"\"{EscapeCsv(entry.LakeName)}\",\"{EscapeCsv(entry.County)}\",\"{EscapeCsv(entry.RegulationText)}\",{entry.HasCrossReference},{entry.IsCompoundEntry},{entry.SpeciesRegulations.Count}");
            }

            await File.WriteAllTextAsync(lakeOutputPath, lakeCsv.ToString());
            AnsiConsole.MarkupLine($"[green]? Exported lakes to CSV: {lakeOutputPath}[/]");

            // Export species regulations
            var speciesOutputPath = Path.Combine(outputDir, "special-regulations-species.csv");
            var speciesCsv = new StringBuilder();
            speciesCsv.AppendLine("LakeName,County,Species,RegulationType,DailyLimit,PossessionLimit,MinimumSize,MaximumSize,ProtectedSlot,IsCatchAndRelease,RegulationDetails");

            foreach (var entry in entries)
            {
                foreach (var species in entry.SpeciesRegulations)
                {
                    speciesCsv.AppendLine($"\"{EscapeCsv(entry.LakeName)}\",\"{EscapeCsv(entry.County)}\",\"{EscapeCsv(species.Species)}\",\"{EscapeCsv(species.RegulationType)}\",{species.DailyLimit?.ToString() ?? ""},{species.PossessionLimit?.ToString() ?? ""},\"{EscapeCsv(species.MinimumSize ?? "")}\",\"{EscapeCsv(species.MaximumSize ?? "")}\",\"{EscapeCsv(species.ProtectedSlot ?? "")}\",{species.IsCatchAndRelease},\"{EscapeCsv(species.RegulationDetails)}\"");
                }
            }

            await File.WriteAllTextAsync(speciesOutputPath, speciesCsv.ToString());
            AnsiConsole.MarkupLine($"[green]? Exported species regulations to CSV: {speciesOutputPath}[/]");
            
            AnsiConsole.MarkupLine($"  Total files size: {(new FileInfo(lakeOutputPath).Length + new FileInfo(speciesOutputPath).Length):N0} bytes");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error exporting to CSV: {ex.Message}[/]");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Escape double quotes by doubling them
        return value.Replace("\"", "\"\"");
    }
}

/// <summary>
/// Represents a parsed lake regulation entry
/// </summary>
public class LakeRegulationEntry
{
    public string LakeName { get; set; } = "";
    public string County { get; set; } = "";
    public string RegulationText { get; set; } = "";
    public bool HasCrossReference { get; set; }
    public bool IsCompoundEntry { get; set; }
    public List<SpeciesRegulation> SpeciesRegulations { get; set; } = new();
}

/// <summary>
/// Represents a species-specific regulation
/// </summary>
public class SpeciesRegulation
{
    public string Species { get; set; } = "";
    public string RegulationType { get; set; } = "";
    public string RegulationDetails { get; set; } = "";
    public int? DailyLimit { get; set; }
    public int? PossessionLimit { get; set; }
    public string? MinimumSize { get; set; }
    public string? MaximumSize { get; set; }
    public string? ProtectedSlot { get; set; }
    public bool IsCatchAndRelease { get; set; }
}
