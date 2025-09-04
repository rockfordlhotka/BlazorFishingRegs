using Azure.AI.OpenAI;
using Azure;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace FishingRegs.Services.Services;

/// <summary>
/// AI-powered service for extracting lake-specific fishing regulations from text
/// </summary>
public class AiLakeRegulationExtractionService : IAiLakeRegulationExtractionService
{
    private readonly ILogger<AiLakeRegulationExtractionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _deploymentName;

    public AiLakeRegulationExtractionService(
        ILogger<AiLakeRegulationExtractionService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var endpoint = _configuration["AzureAI:OpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureAI:OpenAI:Endpoint not configured");
        var apiKey = _configuration["AzureAI:OpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureAI:OpenAI:ApiKey not configured");
        _deploymentName = _configuration["AzureAI:OpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureAI:OpenAI:DeploymentName not configured");
        
        _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<AiLakeRegulationExtractionResult> ExtractLakeRegulationsAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AiLakeRegulationExtractionResult();
        
        try
        {
            _logger.LogInformation("Starting AI-based lake regulation extraction");

            // First, extract the special regulations section
            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                result.ErrorMessage = "Could not find 'Waters With Experimental and Special Regulations' section";
                return result;
            }

            // Parse individual lake entries
            var lakeEntries = ParseLakeEntries(specialRegulationsSection);
            _logger.LogInformation($"Found {lakeEntries.Count} lake entries to process");

            // Process each lake entry
            foreach (var (lakeName, county, regulationText) in lakeEntries)
            {
                try
                {
                    var lakeRegulation = await ExtractSingleLakeRegulationAsync(regulationText, lakeName, county);
                    if (lakeRegulation != null)
                    {
                        result.ExtractedRegulations.Add(lakeRegulation);
                        result.TotalRegulationsExtracted += lakeRegulation.Regulations.SpecialRegulations.Count;
                    }
                    result.TotalLakesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to process lake: {lakeName}");
                    result.ProcessingWarnings.Add($"Failed to process {lakeName}: {ex.Message}");
                }
                
                // Add a small delay to avoid rate limiting
                await Task.Delay(100);
            }

            result.IsSuccess = true;
            _logger.LogInformation($"Successfully extracted regulations for {result.ExtractedRegulations.Count} lakes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lake regulation extraction");
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    public Task<AiLakeRegulation?> ExtractSingleLakeRegulationAsync(string lakeText, string lakeName, string county = "")
    {
        try
        {
            // TODO: Implement actual Azure OpenAI API call
            // For now, create a mock regulation with proper structure for testing
            var mockRegulation = new AiLakeRegulation
            {
                LakeId = 0,
                LakeName = lakeName,
                County = county,
                Regulations = new AiRegulationDetails
                {
                    SpecialRegulations = new List<AiSpecialRegulation>
                    {
                        new AiSpecialRegulation
                        {
                            Species = "Northern Pike",
                            RegulationType = AiRegulationType.SizeLimit,
                            MinimumSize = "24 inches",
                            Notes = "Minimum length requirement"
                        },
                        new AiSpecialRegulation
                        {
                            Species = "Walleye",
                            RegulationType = AiRegulationType.DailyLimit,
                            DailyLimit = 6,
                            Notes = "Daily bag limit"
                        }
                    },
                    GeneralNotes = "Standard state regulations apply unless otherwise specified",
                    IsExperimental = true,
                    LastUpdated = DateTime.UtcNow
                }
            };

            _logger.LogWarning("Azure OpenAI API not yet implemented - returning mock data for lake: {LakeName}", lakeName);
            return Task.FromResult<AiLakeRegulation?>(mockRegulation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting regulation for lake: {lakeName}");
            return Task.FromResult<AiLakeRegulation?>(null);
        }
    }

    public List<(string LakeName, string County, string RegulationText)> ParseLakeEntries(string specialRegulationsSection)
    {
        var lakeEntries = new List<(string, string, string)>();

        try
        {
            // Pattern to match lake entries: LAKE NAME (County) followed by regulations
            var lakePattern = @"^([A-Z][A-Z\s\-,&\.''\d]+(?:\s+(?:including|and|near|Chain|chain|CHAIN)\s+[A-Z\s\-,&\.''\d]*)*)\s*\(([^)]+)\)\s+(.+?)(?=^[A-Z][A-Z\s\-,&\.''\d]+\s*\([^)]+\)|$)";
            
            var matches = Regex.Matches(specialRegulationsSection, lakePattern, RegexOptions.Multiline | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    var lakeName = match.Groups[1].Value.Trim();
                    var county = match.Groups[2].Value.Trim();
                    var regulationText = match.Groups[3].Value.Trim();

                    // Clean up lake name - remove leading symbols and extra spaces
                    lakeName = Regex.Replace(lakeName, @"^[⁕NEW—]*", "").Trim();
                    
                    // Skip if this looks like a section header rather than a lake
                    if (lakeName.Contains("National Wildlife") || lakeName.Contains("Voyageurs") || 
                        lakeName.Length < 3 || regulationText.Length < 10)
                        continue;

                    lakeEntries.Add((lakeName, county, regulationText));
                }
            }

            // If regex approach didn't work well, try a simpler line-by-line approach
            if (lakeEntries.Count < 10)
            {
                lakeEntries.Clear();
                var lines = specialRegulationsSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                string currentLake = "";
                string currentCounty = "";
                var currentRegulation = new List<string>();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
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
                        // Add to current regulation
                        currentRegulation.Add(trimmedLine);
                    }
                }

                // Add the last lake
                if (!string.IsNullOrWhiteSpace(currentLake) && currentRegulation.Count > 0)
                {
                    lakeEntries.Add((currentLake, currentCounty, string.Join(" ", currentRegulation)));
                }
            }

            _logger.LogInformation($"Parsed {lakeEntries.Count} lake entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lake entries");
        }

        return lakeEntries;
    }

    private string ExtractSpecialRegulationsSection(string regulationsText)
    {
        try
        {
            // Find all instances of the special regulations section header
            var startPattern = @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS";
            var matches = Regex.Matches(regulationsText, startPattern, RegexOptions.IgnoreCase);
            
            _logger.LogInformation($"Found {matches.Count} instances of special regulations section header");
            
            Match startMatch;
            if (matches.Count == 0)
            {
                // Try alternative patterns
                startMatch = Regex.Match(regulationsText, @"Special Regulations\s*Lakes \(County\)", RegexOptions.IgnoreCase);
                if (!startMatch.Success)
                {
                    _logger.LogWarning("No special regulations section found");
                    return "";
                }
                _logger.LogInformation("Using alternative pattern match");
            }
            else
            {
                // Use the LAST occurrence (the actual section, not the table of contents reference)
                startMatch = matches[matches.Count - 1];
                _logger.LogInformation($"Using last match at index {startMatch.Index} (of {matches.Count} total matches)");
                
                // Log first few characters of each match for debugging
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    var context = regulationsText.Substring(match.Index, Math.Min(100, regulationsText.Length - match.Index));
                    _logger.LogDebug($"Match {i + 1} at index {match.Index}: {context.Replace('\n', ' ').Replace('\r', ' ').Substring(0, Math.Min(80, context.Length))}...");
                }
            }

            var startIndex = startMatch.Index;

            // Find the end of the section (next major section)
            var endPatterns = new[]
            {
                @"^\s*BORDER WATERS\s*$",           // Must be on its own line  
                @"^\s*BOWFISHING, SPEARING\s*$",    // Must be on its own line
                @"^\s*DARK HOUSE SPEARING\s*$",     // Must be on its own line
                @"^\s*ILLUSTRATED FISH\s*$"         // Must be on its own line
            };

            var endIndex = regulationsText.Length;
            foreach (var pattern in endPatterns)
            {
                var endMatch = Regex.Match(regulationsText.Substring(startIndex), pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (endMatch.Success)
                {
                    endIndex = Math.Min(endIndex, startIndex + endMatch.Index);
                    _logger.LogDebug($"Found end pattern '{pattern}' at relative index {endMatch.Index}");
                }
            }

            var sectionText = regulationsText.Substring(startIndex, endIndex - startIndex);
            _logger.LogInformation($"Extracted section of {sectionText.Length} characters");
            
            // Clean up the text
            sectionText = Regex.Replace(sectionText, @"Page \d+.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
            sectionText = Regex.Replace(sectionText, @"\d+\s+2025 Minnesota Fishing Regulations.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
            
            var cleanedLength = sectionText.Trim().Length;
            _logger.LogInformation($"Cleaned section length: {cleanedLength} characters");
            
            if (cleanedLength > 0)
            {
                var preview = sectionText.Trim().Substring(0, Math.Min(200, cleanedLength));
                _logger.LogDebug($"Section preview: {preview.Replace('\n', ' ').Replace('\r', ' ')}");
            }
            
            return sectionText.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting special regulations section");
            return "";
        }
    }
}
