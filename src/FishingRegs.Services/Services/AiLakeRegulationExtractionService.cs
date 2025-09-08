using Azure.AI.OpenAI;
using Azure;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using OpenAI.Chat;

namespace FishingRegs.Services.Services;

/// <summary>
/// AI-powered service for extracting lake-specific fishing regulations from text
/// </summary>
public class AiLakeRegulationExtractionService : IAiLakeRegulationExtractionService
{
    private readonly ILogger<AiLakeRegulationExtractionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ChatClient _chatClient;

    public AiLakeRegulationExtractionService(
        ILogger<AiLakeRegulationExtractionService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var endpoint = _configuration["AzureAI:OpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureAI:OpenAI:Endpoint not configured");
        var apiKey = _configuration["AzureAI:OpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureAI:OpenAI:ApiKey not configured");
        var deploymentName = _configuration["AzureAI:OpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureAI:OpenAI:DeploymentName not configured");
        
        var azureOpenAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = azureOpenAIClient.GetChatClient(deploymentName);
    }

    public async Task<AiLakeRegulationExtractionResult> ExtractLakeRegulationsAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AiLakeRegulationExtractionResult();
        
        try
        {
            _logger.LogInformation("Starting AI-based lake regulation extraction");

            // Let AI extract the entire special regulations section and parse lake entries
            result = await ExtractAllLakeRegulationsWithAI(regulationsText);
            
            _logger.LogInformation("Successfully extracted regulations for {LakeCount} lakes", result.ExtractedRegulations.Count);
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

    public async Task<AiLakeRegulationExtractionResult> ExtractLakeRegulationsStreamAsync(
        string regulationsText,
        Func<AiLakeRegulation, Task> onLakeProcessed,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AiLakeRegulationExtractionResult();
        
        try
        {
            _logger.LogInformation("Starting AI-based lake regulation extraction with streaming processing");

            // First get all regulations
            var fullResult = await ExtractAllLakeRegulationsWithAI(regulationsText);
            
            // Process each lake immediately
            foreach (var lakeRegulation in fullResult.ExtractedRegulations)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logger.LogInformation("Processing lake: {LakeName} in real-time", lakeRegulation.LakeName);
                    
                    result.ExtractedRegulations.Add(lakeRegulation);
                    result.TotalRegulationsExtracted += lakeRegulation.Regulations?.SpecialRegulations?.Count ?? 0;
                    
                    // Immediately process this lake (call database population)
                    await onLakeProcessed(lakeRegulation);
                    
                    _logger.LogInformation("Completed processing lake: {LakeName}", lakeRegulation.LakeName);
                    result.TotalLakesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process lake: {LakeName}", lakeRegulation.LakeName);
                    result.ProcessingWarnings.Add($"Failed to process {lakeRegulation.LakeName}: {ex.Message}");
                }
                
                // Add a small delay to avoid overwhelming the database
                await Task.Delay(100, cancellationToken);
            }

            result.IsSuccess = true;
            _logger.LogInformation("Successfully extracted and processed regulations for {LakeCount} lakes in streaming mode", result.ExtractedRegulations.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Lake regulation extraction was cancelled");
            result.ErrorMessage = "Processing was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming lake regulation extraction");
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Uses AI to extract all lake regulations from the text in one comprehensive operation
    /// </summary>
    private async Task<AiLakeRegulationExtractionResult> ExtractAllLakeRegulationsWithAI(string regulationsText)
    {
        var result = new AiLakeRegulationExtractionResult();
        
        try
        {
            // Build comprehensive prompt for extracting all lake regulations at once
            var prompt = BuildComprehensiveLakeExtractionPrompt(regulationsText);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation documents and extracting structured data. 
You excel at understanding document structure, identifying lake names from complex text, and parsing regulation content.
Your job is to find and extract ALL lake-specific regulations from the provided text, handling various formatting inconsistencies.
Focus on the 'Waters With Experimental and Special Regulations' section if present.
Return a comprehensive JSON structure with all found lakes and their regulations."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 4000, // Increased for comprehensive extraction
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text;
            
            _logger.LogInformation("Received AI response for comprehensive lake extraction (length: {Length})", jsonContent.Length);

            // Parse the comprehensive JSON response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new RegulationTypeConverter() }
            };
            
            var extractionResult = JsonSerializer.Deserialize<AiLakeRegulationExtractionResult>(jsonContent, options);
            
            if (extractionResult != null)
            {
                result = extractionResult;
                result.IsSuccess = true;
                
                // Set metadata
                result.TotalLakesProcessed = result.ExtractedRegulations.Count;
                result.TotalRegulationsExtracted = result.ExtractedRegulations.Sum(lr => lr.Regulations?.SpecialRegulations?.Count ?? 0);
                
                _logger.LogInformation("Successfully parsed {LakeCount} lakes with {RegulationCount} total regulations", 
                    result.TotalLakesProcessed, result.TotalRegulationsExtracted);
            }
            else
            {
                result.ErrorMessage = "Failed to parse AI response";
                _logger.LogError("Failed to deserialize AI response: {Response}", jsonContent);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from AI");
            result.ErrorMessage = $"JSON parsing error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in comprehensive AI extraction");
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }

    /// <summary>
    /// Builds a comprehensive prompt for extracting all lake regulations
    /// </summary>
    private string BuildComprehensiveLakeExtractionPrompt(string regulationsText)
    {
        // Limit text size to avoid token limits while preserving structure
        var textPreview = regulationsText.Length > 20000 
            ? regulationsText[..20000] + "\n\n[...text truncated for processing...]"
            : regulationsText;
            
        return $@"Analyze this fishing regulations document and extract ALL lake-specific regulations. 
Focus primarily on the 'Waters With Experimental and Special Regulations' section if present.

DOCUMENT TEXT:
{textPreview}

EXTRACTION REQUIREMENTS:
1. Find ALL lakes mentioned with specific regulations (not just general rules)
2. Extract the exact lake names - clean up but preserve the essential name
3. Identify the county for each lake (usually in parentheses after lake name)
4. Extract all species-specific regulations for each lake
5. Handle various text formatting issues (regulations before/after lake names, mixed content, etc.)
6. Parse numeric limits, size restrictions, seasonal information, and special rules
7. Normalize fish species names (e.g., ""bass"" → ""Largemouth Bass"", ""pike"" → ""Northern Pike"")

COMMON PATTERNS TO HANDLE:
- ""LAKE NAME (County): regulation text""
- ""regulation text. LAKE NAME (County)""  
- ""LAKE NAME including inlet (County)""
- ""regulation text for multiple species. LAKE NAME and connected waters (County)""

RETURN FORMAT:
Return a JSON object matching this exact schema:

{{
  ""isSuccess"": true,
  ""extractedRegulations"": [
    {{
      ""lakeId"": 0,
      ""lakeName"": ""Clean Lake Name"",
      ""county"": ""County Name"",
      ""regulations"": {{
        ""specialRegulations"": [
          {{
            ""species"": ""Standardized Species Name"",
            ""regulationType"": ""DailyLimit|PossessionLimit|SizeLimit|ProtectedSlot|CatchAndRelease|Seasonal|Combined"",
            ""dailyLimit"": number_or_null,
            ""possessionLimit"": number_or_null,
            ""minimumSize"": ""size_with_units"" or null,
            ""maximumSize"": ""size_with_units"" or null,
            ""protectedSlot"": ""size_range"" or null,
            ""seasonInfo"": ""season_info"" or null,
            ""catchAndRelease"": true_or_false,
            ""notes"": ""clean_additional_details""
          }}
        ],
        ""generalNotes"": ""overall_lake_notes"",
        ""isExperimental"": false,
        ""lastUpdated"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
      }}
    }}
  ],
  ""errorMessage"": """",
  ""totalLakesProcessed"": 0,
  ""totalRegulationsExtracted"": 0,
  ""processingWarnings"": []
}}

SPECIES NAME STANDARDIZATION:
- ""bass"" → ""Largemouth Bass"" (unless context suggests otherwise)
- ""pike"" → ""Northern Pike""
- ""trout"" → ""Brook Trout"" (unless specified as rainbow, brown, lake, etc.)
- ""salmon"" → ""Salmon"" (or specific type if mentioned)
- ""walleye"" → ""Walleye""
- ""muskie"" or ""muskellunge"" → ""Muskellunge""
- ""perch"" → ""Yellow Perch""
- ""crappie"" → ""Crappie"" (or Black/White Crappie if specified)

REGULATION TYPE SELECTION:
- Use ""Combined"" when multiple regulation types apply to one species
- Use ""CatchAndRelease"" for catch-and-release only rules
- Use ""ProtectedSlot"" for size ranges that must be released
- Use ""DailyLimit"" for bag limits per day
- Use ""PossessionLimit"" for possession limits
- Use ""SizeLimit"" for minimum/maximum size restrictions
- Use ""Seasonal"" for season-specific rules

Be thorough and accurate. Extract every lake with specific regulations, not just examples.";
    }

    public async Task<AiLakeRegulation?> ExtractSingleLakeRegulationAsync(string lakeText, string lakeName, string county = "")
    {
        try
        {
            _logger.LogInformation("Extracting regulations for lake: {LakeName} using Azure OpenAI", lakeName);
            
            // Build the prompt for extracting structured regulation data
            var prompt = BuildSingleLakeExtractionPrompt(lakeText, lakeName, county);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation text and extracting structured data. 
Extract fishing regulation information from the provided lake text and return it as valid JSON matching the specified schema.
Focus on species-specific regulations like daily limits, size limits, possession limits, seasonal restrictions, and special rules.
Clean and standardize the data during extraction - don't just copy raw text."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 1500,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text;
            
            _logger.LogInformation("OpenAI response for {LakeName}: {Response}", lakeName, jsonContent);

            // Parse the JSON response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new RegulationTypeConverter() }
            };
                
            var regulation = JsonSerializer.Deserialize<AiLakeRegulation>(jsonContent, options);

            if (regulation != null)
            {
                // Ensure basic properties are set correctly
                regulation.LakeName = lakeName; // Always use the provided lake name
                regulation.County = county;
                regulation.Regulations ??= new AiRegulationDetails();
                regulation.Regulations.LastUpdated = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully extracted {Count} regulations for lake: {LakeName}", 
                    regulation.Regulations.SpecialRegulations?.Count ?? 0, lakeName);
            }

            return regulation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting regulation for lake: {LakeName}", lakeName);
            return null;
        }
    }

    private string BuildSingleLakeExtractionPrompt(string lakeText, string lakeName, string county)
    {
        return $@"Extract fishing regulation information from the following lake regulation text.

Lake Name: {lakeName}
County: {county}

Regulation Text:
{lakeText}

IMPORTANT INSTRUCTIONS:
1. The lake name is ""{lakeName}"" - use this exactly as provided
2. Clean and standardize all extracted data:
   - Standardize fish species names (e.g., ""bass"" → ""Largemouth Bass"")
   - Extract clean numeric values for limits
   - Normalize size measurements to include units
   - Clean up notes and descriptions
3. If the regulation text contains mixed content (lake names + regulations), extract ONLY the regulation content
4. Be precise with regulation types and categorization

Return the data as JSON matching this exact schema:
{{
  ""lakeId"": 0,
  ""lakeName"": ""{lakeName}"",
  ""county"": ""{county}"",
  ""regulations"": {{
    ""specialRegulations"": [
      {{
        ""species"": ""Standardized Species Name"",
        ""regulationType"": ""DailyLimit|PossessionLimit|SizeLimit|ProtectedSlot|CatchAndRelease|Seasonal|Combined"",
        ""dailyLimit"": null_or_number,
        ""possessionLimit"": null_or_number,
        ""minimumSize"": ""size_with_units"" or null,
        ""maximumSize"": ""size_with_units"" or null,
        ""protectedSlot"": ""size_range"" or null,
        ""seasonInfo"": ""season_info"" or null,
        ""catchAndRelease"": true_or_false,
        ""notes"": ""clean_additional_details""
      }}
    ],
    ""generalNotes"": ""general_notes_about_lake"",
    ""isExperimental"": true_or_false,
    ""lastUpdated"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
  }}
}}

STANDARDIZATION RULES:
- Extract each fish species as a separate regulation entry
- Use standardized species names (Northern Pike, Largemouth Bass, etc.)
- Include units for all size measurements (e.g., ""15 inches"", not just ""15"")
- Set catchAndRelease to true for catch-and-release only regulations
- Use ""Combined"" regulationType when multiple rules apply to one species
- Keep notes concise but informative";
    }

    // Simplified parsing methods - remove complex regex logic
    public List<(string LakeName, string County, string RegulationText)> ParseLakeEntries(string specialRegulationsSection)
    {
        // This method is now primarily for backward compatibility
        // The main extraction should use the comprehensive AI approach
        var lakeEntries = new List<(string, string, string)>();

        try
        {
            // Simple extraction for basic lake entries - let AI handle complex parsing
            var pattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([^)]+)\)\s*:?\s*([^A-Z]*?)(?=[A-Z][A-Z\s\-,&\.''\d]+?\s*\(|$)";
            var matches = Regex.Matches(specialRegulationsSection, pattern, RegexOptions.Multiline);
            
            foreach (Match match in matches)
            {
                var lakeName = match.Groups[1].Value.Trim();
                var county = match.Groups[2].Value.Trim();
                var regulationText = match.Groups[3].Value.Trim();
                
                // Basic filtering
                if (lakeName.Length > 3 && !lakeName.Contains("National Wildlife", StringComparison.OrdinalIgnoreCase))
                {
                    lakeEntries.Add((lakeName, county, regulationText));
                }
            }

            _logger.LogInformation("Parsed {LakeEntryCount} lake entries using simplified parsing", lakeEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lake entries");
        }

        return lakeEntries;
    }

    // Remove most of the complex text preprocessing methods
    // Let AI handle the text understanding
}

/// <summary>
/// Custom JSON converter for AiRegulationType enum that handles case-insensitive conversion
/// </summary>
public class RegulationTypeConverter : JsonConverter<AiRegulationType>
{
    public override AiRegulationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return AiRegulationType.Combined; // Default fallback
        }

        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return AiRegulationType.Combined;
        }

        // Try exact match first
        if (Enum.TryParse<AiRegulationType>(value, true, out var result))
        {
            return result;
        }

        // Try common variations and mappings
        var normalizedValue = value.ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        
        return normalizedValue switch
        {
            "dailylimit" or "daily" => AiRegulationType.DailyLimit,
            "possessionlimit" or "possession" => AiRegulationType.PossessionLimit,
            "sizelimit" or "size" or "minsize" or "maxsize" => AiRegulationType.SizeLimit,
            "protectedslot" or "slotlimit" or "slot" => AiRegulationType.ProtectedSlot,
            "catchandrelease" or "catchrelease" or "release" => AiRegulationType.CatchAndRelease,
            "seasonal" or "season" or "closed" => AiRegulationType.Seasonal,
            "combined" or "multiple" or "special" => AiRegulationType.Combined,
            _ => AiRegulationType.Combined // Default fallback
        };
    }

    public override void Write(Utf8JsonWriter writer, AiRegulationType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
