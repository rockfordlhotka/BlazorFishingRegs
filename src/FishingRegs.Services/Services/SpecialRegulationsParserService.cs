using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FishingRegs.Data;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Logging;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for parsing special regulations text files without AI
/// </summary>
public class SpecialRegulationsParserService : ISpecialRegulationsParserService
{
    private readonly ILogger<SpecialRegulationsParserService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public SpecialRegulationsParserService(
        ILogger<SpecialRegulationsParserService> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<SpecialRegulationsParseResult> ParseSpecialRegulationsAsync(
        string regulationsText,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SpecialRegulationsParseResult { IsSuccess = true };

        try
        {
            _logger.LogInformation("Starting special regulations parsing");

            // Extract the special regulations section
            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                result.ErrorMessage = "Could not find special regulations section in text";
                result.IsSuccess = false;
                return result;
            }

            // Parse lake entries
            result.ParsedLakes = ParseLakeEntries(specialRegulationsSection);
            result.TotalLakesParsed = result.ParsedLakes.Count;
            result.TotalSpeciesRegulationsParsed = result.ParsedLakes.Sum(l => l.SpeciesRegulations.Count);

            _logger.LogInformation($"Parsed {result.TotalLakesParsed} lakes with {result.TotalSpeciesRegulationsParsed} species regulations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing special regulations");
            result.ErrorMessage = ex.Message;
            result.IsSuccess = false;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<RegulationClearResult> ClearPreviousRegulationsAsync(
        int regulationYear,
        CancellationToken cancellationToken = default)
    {
        var result = new RegulationClearResult { IsSuccess = true };

        try
        {
            _logger.LogInformation($"Clearing regulations for year {regulationYear}");

            // Get all regulations for the specified year
            var allRegulations = await _unitOfWork.FishingRegulations.GetAllAsync(cancellationToken);
            var regulationsToDelete = allRegulations.Where(r => r.RegulationYear == regulationYear).ToList();

            result.RegulationsDeleted = regulationsToDelete.Count;
            result.WaterBodiesAffected = regulationsToDelete.Select(r => r.WaterBodyId).Distinct().Count();

            // Delete regulations
            foreach (var regulation in regulationsToDelete)
            {
                _unitOfWork.FishingRegulations.Remove(regulation);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Cleared {result.RegulationsDeleted} regulations affecting {result.WaterBodiesAffected} water bodies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error clearing regulations for year {regulationYear}");
            result.ErrorMessage = ex.Message;
            result.IsSuccess = false;
        }

        return result;
    }

    private string ExtractSpecialRegulationsSection(string regulationsText)
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
                    _logger.LogDebug($"Found section start using pattern: {pattern}");
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
                        _logger.LogDebug($"Found section end using pattern: {pattern}");
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
            _logger.LogError(ex, "Error extracting section");
            return "";
        }
    }

    private string CleanText(string text)
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

    private List<ParsedLakeEntry> ParseLakeEntries(string specialRegulationsSection)
    {
        var lakeEntries = new List<ParsedLakeEntry>();

        try
        {
            // Split into lines for easier processing
            var lines = specialRegulationsSection.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            _logger.LogDebug($"Processing {lines.Count} lines of text");

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
                            var entry = CreateLakeEntry(currentLakeName, currentCounty, regulationText);
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
                    var entry = CreateLakeEntry(currentLakeName, currentCounty, regulationText);
                    lakeEntries.Add(entry);
                }
            }

            _logger.LogInformation($"Successfully parsed {lakeEntries.Count} lake entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lake entries");
        }

        return lakeEntries;
    }

    private ParsedLakeEntry CreateLakeEntry(string lakeName, string county, string regulationText)
    {
        var entry = new ParsedLakeEntry
        {
            LakeName = lakeName,
            County = county,
            RegulationText = regulationText,
            HasCrossReference = regulationText.Contains("See ", StringComparison.OrdinalIgnoreCase),
            IsCompoundEntry = lakeName.Contains("and connected", StringComparison.OrdinalIgnoreCase) ||
                            lakeName.Contains("including", StringComparison.OrdinalIgnoreCase),
            SpeciesRegulations = ParseSpeciesRegulations(regulationText)
        };

        return entry;
    }

    private List<ParsedSpeciesRegulation> ParseSpeciesRegulations(string regulationText)
    {
        var regulations = new List<ParsedSpeciesRegulation>();

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
                        var regulation = new ParsedSpeciesRegulation
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing species regulations");
        }

        return regulations;
    }

    private void ParseRegulationDetails(ParsedSpeciesRegulation regulation, string text)
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

        // Parse protected slot (e.g., "all from 24-36" must be released")
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

    private string NormalizeSpeciesName(string species)
    {
        // Convert to title case and handle special cases
        var normalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(species.ToLower());
        
        // Handle specific replacements
        normalized = normalized switch
        {
            "Pike" => "Northern Pike",
            _ => normalized
        };

        return normalized;
    }

    private string CleanLakeName(string lakeName)
    {
        // Remove leading symbols
        lakeName = Regex.Replace(lakeName, @"^[?NEW—•\*\s]+", "").Trim();
        
        // Clean up extra whitespace
        lakeName = Regex.Replace(lakeName, @"\s+", " ").Trim();
        
        return lakeName;
    }

    private bool IsHeaderOrSection(string text)
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
}
