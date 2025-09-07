using Azure.AI.OpenAI;
using Azure;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ClientModel;
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

            // First, extract the special regulations section
            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            if (string.IsNullOrWhiteSpace(specialRegulationsSection))
            {
                result.ErrorMessage = "Could not find 'Waters With Experimental and Special Regulations' section";
                return result;
            }

            // Parse individual lake entries
            var lakeEntries = ParseLakeEntries(specialRegulationsSection);
            _logger.LogInformation($"Found {lakeEntries.Count} lake entries to process with streaming");

            // Process each lake entry immediately after extraction
            foreach (var (lakeName, county, regulationText) in lakeEntries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logger.LogInformation("Processing lake: {LakeName} in real-time", lakeName);
                    
                    var lakeRegulation = await ExtractSingleLakeRegulationAsync(regulationText, lakeName, county);
                    if (lakeRegulation != null)
                    {
                        result.ExtractedRegulations.Add(lakeRegulation);
                        result.TotalRegulationsExtracted += lakeRegulation.Regulations.SpecialRegulations.Count;
                        
                        // Immediately process this lake (call database population)
                        await onLakeProcessed(lakeRegulation);
                        
                        _logger.LogInformation("Completed processing lake: {LakeName}", lakeName);
                    }
                    result.TotalLakesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to process lake: {lakeName}");
                    result.ProcessingWarnings.Add($"Failed to process {lakeName}: {ex.Message}");
                }
                
                // Add a small delay to avoid rate limiting
                await Task.Delay(100, cancellationToken);
            }

            result.IsSuccess = true;
            _logger.LogInformation($"Successfully extracted and processed regulations for {result.ExtractedRegulations.Count} lakes in streaming mode");
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

    public async Task<AiLakeRegulation?> ExtractSingleLakeRegulationAsync(string lakeText, string lakeName, string county = "")
    {
        try
        {
            _logger.LogInformation("Extracting regulations for lake: {LakeName} using Azure OpenAI", lakeName);
            
            // Log the input text for debugging
            var previewText = lakeText.Length > 200 ? lakeText.Substring(0, 200) + "..." : lakeText;
            _logger.LogInformation("Input text for {LakeName}: {Text}", lakeName, previewText);
            
            // Pre-process the lake text to clean up any formatting issues
            var cleanedLakeText = PreprocessSingleLakeText(lakeText, lakeName);
            if (cleanedLakeText != lakeText)
            {
                _logger.LogInformation("Preprocessed text for {LakeName}: {Text}", lakeName, cleanedLakeText.Substring(0, Math.Min(200, cleanedLakeText.Length)));
            }
            
            // Build the prompt for extracting structured regulation data
            var prompt = BuildRegulationExtractionPrompt(cleanedLakeText, lakeName, county);
            
            // Make the API call to Azure OpenAI using the newer ChatClient API
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation text and extracting structured data. 
Extract fishing regulation information from the provided lake text and return it as valid JSON matching the specified schema.
Focus on species-specific regulations like daily limits, size limits, possession limits, seasonal restrictions, and special rules.
The lake name is provided separately and should not be changed. Focus only on extracting regulation content from the text.
If the regulation text contains the lake name mixed with regulations, ignore the lake name parts and extract only the regulation content.
If no specific regulations are mentioned, return an empty regulations array."),
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

            // Parse the JSON response with more robust error handling
            AiLakeRegulation? regulation;
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new RegulationTypeConverter() }
                };
                
                regulation = JsonSerializer.Deserialize<AiLakeRegulation>(jsonContent, options);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON response for {LakeName}. JSON: {Json}", lakeName, jsonContent);
                
                // Try to create a minimal regulation entry with the raw text
                regulation = new AiLakeRegulation
                {
                    LakeName = lakeName,
                    County = county,
                    Regulations = new AiRegulationDetails
                    {
                        GeneralNotes = $"Raw regulation text: {cleanedLakeText}",
                        SpecialRegulations = new List<AiSpecialRegulation>()
                    }
                };
            }

            if (regulation != null)
            {
                // Ensure basic properties are set correctly
                regulation.LakeName = lakeName; // Always use the provided lake name
                regulation.County = county;
                regulation.Regulations.LastUpdated = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully extracted {Count} regulations for lake: {LakeName}", 
                    regulation.Regulations.SpecialRegulations.Count, lakeName);
                    
                // Log extracted regulations for debugging
                foreach (var reg in regulation.Regulations.SpecialRegulations)
                {
                    _logger.LogDebug("Extracted regulation: {Species} - {Type} - {Notes}", 
                        reg.Species, reg.RegulationType, reg.Notes?.Substring(0, Math.Min(50, reg.Notes?.Length ?? 0)));
                }
            }

            return regulation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting regulation for lake: {LakeName}", lakeName);
            return null;
        }
    }

    /// <summary>
    /// Preprocesses text for a single lake to clean up formatting issues
    /// </summary>
    private string PreprocessSingleLakeText(string lakeText, string lakeName)
    {
        if (string.IsNullOrWhiteSpace(lakeText))
            return lakeText;
            
        var cleaned = lakeText.Trim();
        
        // If the text starts with regulation content and ends with the lake name, this is likely malformed
        // Example: "Largemouth bass: catch-and-release only. Northern pike: ... ANNIE BATTLE LAKE including..."
        if (cleaned.Contains(lakeName, StringComparison.OrdinalIgnoreCase))
        {
            // Find where the lake name appears in the text
            var lakeNameIndex = cleaned.IndexOf(lakeName, StringComparison.OrdinalIgnoreCase);
            
            // If lake name appears in the middle or end of the text, everything before it is likely regulations
            if (lakeNameIndex > 0)
            {
                var regulationPart = cleaned.Substring(0, lakeNameIndex).Trim();
                
                // Check if the regulation part contains fish species (indicator of regulation text)
                if (Regex.IsMatch(regulationPart, @"\b(bass|pike|trout|salmon|walleye|muskie|perch|crappie|bluegill)\b", RegexOptions.IgnoreCase))
                {
                    _logger.LogInformation("Detected malformed text for {LakeName}, extracting regulation part: {RegulationPart}", 
                        lakeName, regulationPart.Substring(0, Math.Min(100, regulationPart.Length)));
                    return regulationPart;
                }
            }
        }
        
        // Remove any duplicate lake name references from the text
        var pattern = Regex.Escape(lakeName);
        cleaned = Regex.Replace(cleaned, pattern, "", RegexOptions.IgnoreCase).Trim();
        
        // Clean up any remaining artifacts
        cleaned = Regex.Replace(cleaned, @"including\s+inlet\s+to\s+\w+.*$", "", RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"and\s+outlet\s+to\s+\w+.*$", "", RegexOptions.IgnoreCase).Trim();
        
        return cleaned;
    }

    private string BuildRegulationExtractionPrompt(string lakeText, string lakeName, string county)
    {
        return $@"Extract fishing regulation information from the following lake regulation text.

Lake Name: {lakeName}
County: {county}

Regulation Text:
{lakeText}

IMPORTANT PARSING INSTRUCTIONS:
1. The lake name is ""{lakeName}"" - this is definitive and should NOT be changed
2. The regulation text may contain the lake name mixed with regulations - focus ONLY on the regulation content
3. If the text appears to be malformed (e.g., regulations mixed with lake names), extract ONLY the regulation parts
4. Ignore any repetition of the lake name within the regulation text
5. Focus on fish species, limits, sizes, and restrictions

Return the data as JSON matching this exact schema:
{{
  ""lakeId"": 0,
  ""lakeName"": ""{lakeName}"",
  ""county"": ""{county}"",
  ""regulations"": {{
    ""specialRegulations"": [
      {{
        ""species"": ""Fish Species Name"",
        ""regulationType"": ""DailyLimit"",
        ""dailyLimit"": null or number,
        ""possessionLimit"": null or number,
        ""minimumSize"": ""size with units"" or null,
        ""maximumSize"": ""size with units"" or null,
        ""protectedSlot"": ""size range"" or null,
        ""seasonInfo"": ""season info"" or null,
        ""catchAndRelease"": true or false,
        ""notes"": ""additional regulation details""
      }}
    ],
    ""generalNotes"": ""general notes about the lake regulations"",
    ""isExperimental"": true or false,
    ""lastUpdated"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
  }}
}}

Extraction rules:
1. Extract each fish species as a separate regulation entry
2. For regulationType, use EXACTLY one of: ""DailyLimit"", ""PossessionLimit"", ""SizeLimit"", ""ProtectedSlot"", ""CatchAndRelease"", ""Seasonal"", ""Combined""
3. Extract numeric values for limits and sizes (include units for sizes)
4. Be precise with species names (e.g., ""Northern Pike"", ""Walleye"", ""Largemouth Bass"")
5. For catch-and-release regulations, set catchAndRelease to true and regulationType to ""CatchAndRelease""
6. For size restrictions like ""24-36 inches must be released"", use regulationType ""ProtectedSlot"" and set protectedSlot to ""24-36 inches""
7. If text mentions possession limits, extract the number and use regulationType ""PossessionLimit""
8. Include relevant context in the notes field but keep it concise
9. If the text is unclear or appears to contain the lake name mixed with regulations, focus on extracting the regulation content only
10. Use ""Combined"" as regulationType when multiple regulation types apply to a species

Example for the given context:
- ""Largemouth bass: catch-and-release only"" → species: ""Largemouth Bass"", regulationType: ""CatchAndRelease"", catchAndRelease: true
- ""Northern pike: all from 24-36 inches must be immediately released. Possession limit 3, only 1 over 36 inches"" → 
  Two entries: 
  1) species: ""Northern Pike"", regulationType: ""ProtectedSlot"", protectedSlot: ""24-36 inches""
  2) species: ""Northern Pike"", regulationType: ""PossessionLimit"", possessionLimit: 3, notes: ""only 1 over 36 inches""";
    }

    public List<(string LakeName, string County, string RegulationText)> ParseLakeEntries(string specialRegulationsSection)
    {
        var lakeEntries = new List<(string, string, string)>();

        try
        {
            // Normalize the text by combining lines and cleaning up spacing
            var normalizedText = Regex.Replace(specialRegulationsSection, @"\s+", " ").Trim();
            
            // First, let's try to fix common formatting issues where regulations appear before lake names
            normalizedText = PreprocessRegulationText(normalizedText);
            
            // Use manual approach for better compound entry handling
            // This approach handles cases like "DAM LAKE and connected Lily Lake and Dam Brook (Aitkin)"
            var expectedPatterns = new[]
            {
                // Look for compound entries first (more specific patterns)
                @"([A-Z][^()]*?(?:and connected|including|and|near)[^()]*?)\s*\(([^)]+)\)",
                // Then look for simple lake entries
                @"([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)"
            };

            var foundLakes = new HashSet<int>(); // Track positions to avoid duplicates
            
            foreach (var pattern in expectedPatterns)
            {
                var matches = Regex.Matches(normalizedText, pattern);
                
                foreach (Match match in matches)
                {
                    // Skip if we already processed a lake at this position
                    if (foundLakes.Contains(match.Index))
                        continue;
                        
                    foundLakes.Add(match.Index);
                    
                    var lakeName = match.Groups[1].Value.Trim();
                    var county = match.Groups[2].Value.Trim();
                    
                    // Clean up lake name - remove leading symbols
                    lakeName = Regex.Replace(lakeName, @"^[⁕NEW—]*", "").Trim();
                    
                    // Skip section headers and very short names
                    if (lakeName.Contains("National Wildlife") || 
                        lakeName.Contains("Voyageurs") || 
                        lakeName.Length < 3)
                    {
                        continue;
                    }
                    
                    // Extract regulation text for this lake entry
                    var regulationText = ExtractRegulationTextForLake(normalizedText, match, lakeName, county);
                    
                    // Only add if we have meaningful regulation text (lowered threshold for cross-references)
                    if (!string.IsNullOrWhiteSpace(regulationText) && regulationText.Length >= 5)
                    {
                        lakeEntries.Add((lakeName, county, regulationText));
                    }
                }
            }
            
            // Sort by position in text to maintain order
            lakeEntries = lakeEntries
                .Select(entry => new { 
                    Entry = entry, 
                    Position = normalizedText.IndexOf($"{entry.Item1} ({entry.Item2})", StringComparison.OrdinalIgnoreCase) 
                })
                .Where(x => x.Position >= 0)
                .OrderBy(x => x.Position)
                .Select(x => x.Entry)
                .ToList();

            _logger.LogInformation($"Parsed {lakeEntries.Count} lake entries using improved algorithm");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lake entries");
        }

        return lakeEntries;
    }

    /// <summary>
    /// Preprocesses regulation text to fix common formatting issues
    /// </summary>
    private string PreprocessRegulationText(string text)
    {
        try
        {
            // Look for cases where regulation text appears before lake names
            // Pattern: "regulation text. LAKE NAME (County)"
            var problematicPattern = @"([^.]+(?:bass|pike|trout|salmon|walleye|muskie|perch|crappie)[^.]*\.)\s*([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)";
            var matches = Regex.Matches(text, problematicPattern);
            
            foreach (Match match in matches.Cast<Match>().Reverse()) // Process in reverse to maintain indices
            {
                var regulationText = match.Groups[1].Value.Trim();
                var lakeName = match.Groups[2].Value.Trim();
                var county = match.Groups[3].Value.Trim();
                
                // Reformat to standard format: "LAKE NAME (County): regulation text"
                var correctedEntry = $"{lakeName} ({county}): {regulationText}";
                
                // Replace the problematic text with the corrected format
                text = text.Substring(0, match.Index) + correctedEntry + text.Substring(match.Index + match.Length);
                
                _logger.LogInformation($"Corrected formatting for {lakeName}: moved regulations after lake name");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in preprocessing regulation text");
        }
        
        return text;
    }

    /// <summary>
    /// Extracts regulation text for a specific lake entry
    /// </summary>
    private string ExtractRegulationTextForLake(string normalizedText, Match lakeMatch, string lakeName, string county)
    {
        var startPos = lakeMatch.Index + lakeMatch.Length;
        var endPos = normalizedText.Length;
        
        // Look for a colon immediately after the lake name/county which indicates regulation text follows
        var colonMatch = Regex.Match(normalizedText.Substring(startPos), @"^\s*:\s*");
        if (colonMatch.Success)
        {
            startPos += colonMatch.Length;
        }
        
        // Find the next lake entry to determine where this regulation ends
        var nextLakePattern = @"[A-Z][A-Z\s\-,&\.''\d]*?\s*\([^)]+\)";
        var nextMatch = Regex.Match(normalizedText.Substring(startPos), nextLakePattern);
        if (nextMatch.Success)
        {
            endPos = startPos + nextMatch.Index;
        }
        
        var regulationText = "";
        if (startPos < endPos)
        {
            regulationText = normalizedText.Substring(startPos, endPos - startPos).Trim();
            
            // Clean up regulation text
            regulationText = CleanRegulationText(regulationText);
        }
        
        // If we didn't find regulation text after the lake name, check if it appears before
        if (string.IsNullOrWhiteSpace(regulationText) || regulationText.Length < 10)
        {
            regulationText = ExtractRegulationTextBeforeLake(normalizedText, lakeMatch, lakeName);
        }
        
        return regulationText;
    }

    /// <summary>
    /// Attempts to extract regulation text that appears before the lake name (for malformed entries)
    /// </summary>
    private string ExtractRegulationTextBeforeLake(string normalizedText, Match lakeMatch, string lakeName)
    {
        try
        {
            // Look backwards from the lake match to find regulation text
            var textBeforeLake = normalizedText.Substring(0, lakeMatch.Index);
            
            // Find the last sentence or regulation that might belong to this lake
            // Look for fish species names as indicators
            var fishSpeciesPattern = @"([^.]*(?:bass|pike|trout|salmon|walleye|muskie|perch|crappie|bluegill|sunfish)[^.]*\.)\s*$";
            var match = Regex.Match(textBeforeLake, fishSpeciesPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var regulationText = match.Groups[1].Value.Trim();
                _logger.LogInformation($"Found regulation text before lake name for {lakeName}: {regulationText.Substring(0, Math.Min(50, regulationText.Length))}...");
                return CleanRegulationText(regulationText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error extracting regulation text before lake for {lakeName}");
        }
        
        return "";
    }

    /// <summary>
    /// Cleans up regulation text by removing page headers, footers, and other artifacts
    /// </summary>
    private string CleanRegulationText(string regulationText)
    {
        if (string.IsNullOrWhiteSpace(regulationText))
            return "";
            
        // Remove page headers and footers
        regulationText = Regex.Replace(regulationText, @"Page \d+.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
        regulationText = Regex.Replace(regulationText, @"\d+\s+2025 Minnesota Fishing Regulations.*?888-MINNDNR", "", RegexOptions.IgnoreCase);
        
        // Remove leading colons or spaces
        regulationText = Regex.Replace(regulationText, @"^[:,\s]+", "").Trim();
        
        // Remove trailing periods that might be sentence artifacts
        regulationText = regulationText.TrimEnd('.').Trim();
        
        return regulationText;
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
        var normalizedValue = value.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        
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
