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

    public async Task<AiLakeRegulation?> ExtractSingleLakeRegulationAsync(string lakeText, string lakeName, string county = "")
    {
        try
        {
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured");
            
            var chatClient = _openAIClient.GetChatClient(deploymentName);

            var systemPrompt = @"You are a fishing regulation expert. Extract structured fishing regulation data from the given lake regulation text.

IMPORTANT: Return ONLY valid JSON with no additional text or markdown formatting.

The JSON should follow this exact structure:
{
  ""lakeId"": 0,
  ""lakeName"": ""Lake Name"",
  ""county"": ""County Name"",
  ""regulations"": {
    ""specialRegulations"": [
      {
        ""species"": ""Species Name"",
        ""regulationType"": ""DailyLimit|PossessionLimit|SizeLimit|ProtectedSlot|CatchAndRelease|Seasonal|Combined"",
        ""dailyLimit"": null or number,
        ""possessionLimit"": null or number,
        ""minimumSize"": ""size string"" or null,
        ""maximumSize"": ""size string"" or null,
        ""protectedSlot"": ""slot description"" or null,
        ""seasonInfo"": ""season information"" or null,
        ""catchAndRelease"": true or false,
        ""notes"": ""regulation details""
      }
    ],
    ""generalNotes"": ""general information about the lake"",
    ""isExperimental"": true or false,
    ""lastUpdated"": ""2025-09-04T00:00:00Z""
  }
}

Parse these common regulation patterns:
- ""daily limit X"" -> dailyLimit: X
- ""possession limit X"" -> possessionLimit: X
- ""all from X-Y"" must be immediately released"" -> protectedSlot
- ""catch-and-release only"" -> catchAndRelease: true
- ""minimum size limit X"" -> minimumSize
- ""only 1 over X"" -> special size restrictions
- Species names: walleye, northern pike, bass (largemouth/smallmouth), sunfish, crappie, trout, etc.

Extract the county from parentheses if present in the lake name.";

            var userPrompt = $@"Lake Name: {lakeName}
County: {county}

Regulation Text:
{lakeText}

Extract the fishing regulations and return as JSON:";

            // TODO: Implement Azure OpenAI chat completion
            // For now, return a mock result to allow compilation
            _logger.LogWarning("AI extraction not yet implemented - returning mock data");
            
            var jsonResponse = "[]"; // Empty array for now
            
            // Clean up the response in case there's any markdown formatting
            if (jsonResponse.StartsWith("```json"))
            {
                jsonResponse = jsonResponse.Substring(7);
            }
            if (jsonResponse.EndsWith("```"))
            {
                jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
            }
            jsonResponse = jsonResponse.Trim();

            var lakeRegulation = JsonSerializer.Deserialize<AiLakeRegulation>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Set the lake name and county if they weren't properly extracted
            if (lakeRegulation != null)
            {
                if (string.IsNullOrWhiteSpace(lakeRegulation.LakeName))
                    lakeRegulation.LakeName = lakeName;
                if (string.IsNullOrWhiteSpace(lakeRegulation.County))
                    lakeRegulation.County = county;
            }

            return lakeRegulation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting regulation for lake: {lakeName}");
            return null;
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
}
