using Azure.AI.OpenAI;
using Azure;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using OpenAI.Chat;

namespace FishingRegs.Services.Services;

/// <summary>
/// Comprehensive AI-powered service for extracting ALL data from fishing regulations document
/// Extracts: counties, water bodies, fish species, and regulations systematically
/// </summary>
public class ComprehensiveDataExtractionService : IComprehensiveDataExtractionService
{
    private readonly ILogger<ComprehensiveDataExtractionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ChatClient _chatClient;

    public ComprehensiveDataExtractionService(
        ILogger<ComprehensiveDataExtractionService> logger,
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

    /// <summary>
    /// Extracts all counties mentioned in the fishing regulations document
    /// </summary>
    public async Task<ComprehensiveExtractionResult<List<CountyData>>> ExtractAllCountiesAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ComprehensiveExtractionResult<List<CountyData>>();
        
        try
        {
            _logger.LogInformation("Starting comprehensive county extraction");

            var prompt = BuildCountyExtractionPrompt(regulationsText);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation documents and extracting geographic data. 
Your task is to find ALL counties mentioned throughout the entire document and return them as a clean, standardized list.
Be thorough and systematic - this will be used to populate a database of Minnesota counties.
You must respond with valid JSON format only."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 2000,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text;
            
            _logger.LogInformation("Received AI response for county extraction (length: {Length})", jsonContent.Length);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            result = JsonSerializer.Deserialize<ComprehensiveExtractionResult<List<CountyData>>>(jsonContent, options) 
                ?? new ComprehensiveExtractionResult<List<CountyData>>();
            
            result.IsSuccess = true;
            _logger.LogInformation("Successfully extracted {Count} counties", result.Data?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during county extraction");
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

    /// <summary>
    /// Extracts all water bodies (lakes, rivers, streams) with their counties
    /// </summary>
    public async Task<ComprehensiveExtractionResult<List<WaterBodyData>>> ExtractAllWaterBodiesAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ComprehensiveExtractionResult<List<WaterBodyData>>();
        
        try
        {
            _logger.LogInformation("Starting comprehensive water body extraction");

            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            
            if (string.IsNullOrEmpty(specialRegulationsSection))
            {
                _logger.LogWarning("Could not find special regulations section, processing entire document");
                specialRegulationsSection = regulationsText;
            }

            _logger.LogInformation("Processing special regulations section (length: {Length})", specialRegulationsSection.Length);

            var allWaterBodies = new List<WaterBodyData>();
            var chunkSize = 8000;
            
            if (specialRegulationsSection.Length <= chunkSize)
            {
                var chunkResult = await ExtractWaterBodiesFromChunk(specialRegulationsSection, 1, 1);
                if (chunkResult.IsSuccess && chunkResult.Data != null)
                {
                    allWaterBodies.AddRange(chunkResult.Data);
                }
            }
            else
            {
                var chunks = SplitIntoOverlappingChunks(specialRegulationsSection, chunkSize, 1000);
                _logger.LogInformation("Processing {ChunkCount} chunks for water body extraction", chunks.Count);

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkResult = await ExtractWaterBodiesFromChunk(chunks[i], i + 1, chunks.Count);
                    if (chunkResult.IsSuccess && chunkResult.Data != null)
                    {
                        allWaterBodies.AddRange(chunkResult.Data);
                        _logger.LogInformation("Chunk {ChunkNumber}/{TotalChunks}: Extracted {Count} water bodies", 
                            i + 1, chunks.Count, chunkResult.Data.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Chunk {ChunkNumber}/{TotalChunks} failed: {Error}", 
                            i + 1, chunks.Count, chunkResult.ErrorMessage ?? "Unknown error");
                        
                        var fallbackResult = await TryFallbackExtraction(chunks[i], i + 1, chunks.Count);
                        if (fallbackResult.Count > 0)
                        {
                            allWaterBodies.AddRange(fallbackResult);
                            _logger.LogInformation("Chunk {ChunkNumber}/{TotalChunks}: Fallback extracted {Count} water bodies", 
                                i + 1, chunks.Count, fallbackResult.Count);
                        }
                    }
                    
                    await Task.Delay(200);
                }
            }

            var uniqueWaterBodies = allWaterBodies
                .GroupBy(wb => new { wb.Name, wb.County })
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Extracted {TotalCount} water bodies ({UniqueCount} unique)", 
                allWaterBodies.Count, uniqueWaterBodies.Count);

            result.Data = uniqueWaterBodies;
            result.TotalExtracted = uniqueWaterBodies.Count;
            result.IsSuccess = true;
            
            _logger.LogInformation("Successfully extracted {Count} unique water bodies", uniqueWaterBodies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during water body extraction");
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

    /// <summary>
    /// Extracts all fish species mentioned in the regulations
    /// </summary>
    public async Task<ComprehensiveExtractionResult<List<FishSpeciesData>>> ExtractAllFishSpeciesAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ComprehensiveExtractionResult<List<FishSpeciesData>>();
        
        try
        {
            _logger.LogInformation("Starting comprehensive fish species extraction");

            var prompt = BuildFishSpeciesExtractionPrompt(regulationsText);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation documents and extracting fish species data. 
Your task is to find ALL fish species mentioned throughout the document and standardize their names.
Focus on creating a comprehensive list of all fish species that have regulations.
You must respond with valid JSON format only."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 2000,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text;
            
            _logger.LogInformation("Received AI response for fish species extraction (length: {Length})", jsonContent.Length);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            result = JsonSerializer.Deserialize<ComprehensiveExtractionResult<List<FishSpeciesData>>>(jsonContent, options) 
                ?? new ComprehensiveExtractionResult<List<FishSpeciesData>>();
            
            result.IsSuccess = true;
            _logger.LogInformation("Successfully extracted {Count} fish species", result.Data?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during fish species extraction");
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

    /// <summary>
    /// Extracts all regulations per water body - the comprehensive approach
    /// </summary>
    public async Task<ComprehensiveExtractionResult<List<WaterBodyRegulationData>>> ExtractAllRegulationsAsync(string regulationsText)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ComprehensiveExtractionResult<List<WaterBodyRegulationData>>();
        
        try
        {
            _logger.LogInformation("Starting comprehensive regulation extraction");

            var specialRegulationsSection = ExtractSpecialRegulationsSection(regulationsText);
            
            if (string.IsNullOrEmpty(specialRegulationsSection))
            {
                _logger.LogWarning("Could not find special regulations section for regulations, processing entire document");
                specialRegulationsSection = regulationsText;
            }

            _logger.LogInformation("Processing special regulations section for regulations (length: {Length})", specialRegulationsSection.Length);

            var allRegulations = new List<WaterBodyRegulationData>();
            var chunkSize = 20000;
            
            if (specialRegulationsSection.Length <= chunkSize)
            {
                var chunkResult = await ExtractRegulationsFromChunk(specialRegulationsSection, 1, 1);
                if (chunkResult.IsSuccess && chunkResult.Data != null)
                {
                    allRegulations.AddRange(chunkResult.Data);
                }
            }
            else
            {
                var chunks = SplitIntoOverlappingChunks(specialRegulationsSection, chunkSize, 1000);
                _logger.LogInformation("Processing {ChunkCount} chunks for regulation extraction", chunks.Count);

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkResult = await ExtractRegulationsFromChunk(chunks[i], i + 1, chunks.Count);
                    if (chunkResult.IsSuccess && chunkResult.Data != null)
                    {
                        allRegulations.AddRange(chunkResult.Data);
                        _logger.LogInformation("Regulations Chunk {ChunkNumber}/{TotalChunks}: Extracted {Count} water body regulations", 
                            i + 1, chunks.Count, chunkResult.Data.Count);
                    }
                    
                    await Task.Delay(300);
                }
            }

            var uniqueRegulations = allRegulations
                .GroupBy(wr => new { wr.WaterBodyName, wr.County })
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Extracted {TotalCount} regulation entries ({UniqueCount} unique)", 
                allRegulations.Count, uniqueRegulations.Count);

            result.Data = uniqueRegulations;
            result.TotalExtracted = uniqueRegulations.Count;
            result.IsSuccess = true;
            
            _logger.LogInformation("Successfully extracted regulations for {Count} unique water bodies", uniqueRegulations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during regulation extraction");
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

    private string ExtractSpecialRegulationsSection(string regulationsText)
    {
        try
        {
            var lakePatterns = new[]
            {
                @"[A-Z]+ LAKE \([A-Za-z\s]+\)",
                @"[A-Z][A-Z\s]+ LAKE \([A-Za-z\s]+\)",
            };

            int actualContentStart = -1;
            foreach (var pattern in lakePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(regulationsText, pattern);
                if (match.Success)
                {
                    actualContentStart = match.Index;
                    break;
                }
            }

            if (actualContentStart < 0)
            {
                _logger.LogWarning("Could not find actual lake entries, using full document");
                return regulationsText;
            }

            var endPatterns = new[]
            {
                "BORDER WATERS",
                "BOWFISHING", 
                "DARK HOUSE SPEARING",
                "ICE ANGLING",
                "ILLUSTRATED FISH"
            };

            int endIndex = regulationsText.Length;
            foreach (var pattern in endPatterns)
            {
                var foundEndIndex = regulationsText.IndexOf(pattern, actualContentStart + 1000, StringComparison.OrdinalIgnoreCase);
                if (foundEndIndex >= 0 && foundEndIndex < endIndex)
                {
                    endIndex = foundEndIndex;
                }
            }

            var section = regulationsText.Substring(actualContentStart, endIndex - actualContentStart);
            _logger.LogInformation("Extracted special regulations content: {Length} characters (starting from actual lake entries)", 
                section.Length);
            
            return section;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting special regulations section, using full document");
            return regulationsText;
        }
    }

    private List<string> SplitIntoOverlappingChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        int position = 0;

        while (position < text.Length)
        {
            int endPosition = Math.Min(position + chunkSize, text.Length);
            
            if (endPosition < text.Length)
            {
                var nextNewline = text.IndexOf('\n', endPosition - 200);
                if (nextNewline > endPosition - 100 && nextNewline < text.Length)
                {
                    endPosition = nextNewline + 1;
                }
            }

            var chunk = text.Substring(position, endPosition - position);
            chunks.Add(chunk);

            if (endPosition >= text.Length) break;
            
            position = endPosition - overlap;
        }

        return chunks;
    }

    private async Task<ComprehensiveExtractionResult<List<WaterBodyData>>> ExtractWaterBodiesFromChunk(
        string textChunk, 
        int chunkNumber, 
        int totalChunks)
    {
        var result = new ComprehensiveExtractionResult<List<WaterBodyData>>();
        
        try
        {
            var prompt = BuildWaterBodyExtractionPrompt(textChunk, chunkNumber, totalChunks);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation documents and extracting water body data. 
Extract the name, type, and county for each water body. Be systematic and thorough.
You must respond with valid JSON format only. IMPORTANT: Ensure your JSON is complete and properly closed."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 3000,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text.Trim();
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                result.ErrorMessage = "Received empty response from AI";
                result.IsSuccess = false;
                return result;
            }

            // Fix JSON if truncated
            if (!jsonContent.EndsWith('}'))
            {
                jsonContent = FixTruncatedJson(jsonContent, chunkNumber);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            result = JsonSerializer.Deserialize<ComprehensiveExtractionResult<List<WaterBodyData>>>(jsonContent, options) 
                ?? new ComprehensiveExtractionResult<List<WaterBodyData>>();
            
            result.IsSuccess = true;
            result.Data ??= new List<WaterBodyData>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for chunk {ChunkNumber}", chunkNumber);
            result.ErrorMessage = $"JSON parsing error: {ex.Message}";
            result.IsSuccess = false;
            result.Data = new List<WaterBodyData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting water bodies from chunk {ChunkNumber}", chunkNumber);
            result.ErrorMessage = ex.Message;
            result.IsSuccess = false;
            result.Data = new List<WaterBodyData>();
        }

        return result;
    }

    private string FixTruncatedJson(string jsonContent, int chunkNumber)
    {
        _logger.LogWarning("Attempting to fix truncated JSON for chunk {ChunkNumber}", chunkNumber);
        
        // Find last complete entry
        var lastCompleteIndex = jsonContent.LastIndexOf("}}");
        if (lastCompleteIndex > 0)
        {
            jsonContent = jsonContent.Substring(0, lastCompleteIndex + 2);
        }
        
        // Ensure proper closing
        if (!jsonContent.EndsWith(']'))
        {
            jsonContent += "]";
        }
        
        var openBraces = jsonContent.Count(c => c == '{');
        var closeBraces = jsonContent.Count(c => c == '}');
        
        while (closeBraces < openBraces)
        {
            jsonContent += "}";
            closeBraces++;
        }
        
        return jsonContent;
    }

    private async Task<List<WaterBodyData>> TryFallbackExtraction(string textChunk, int chunkNumber, int totalChunks)
    {
        var waterBodies = new List<WaterBodyData>();
        
        try
        {
            _logger.LogInformation("Attempting fallback regex extraction for chunk {ChunkNumber}", chunkNumber);
            
            var lakePattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([^)]+)\)";
            var matches = System.Text.RegularExpressions.Regex.Matches(textChunk, lakePattern);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var name = match.Groups[1].Value.Trim();
                var county = match.Groups[2].Value.Trim();
                
                if (IsValidWaterBodyEntry(name, county))
                {
                    waterBodies.Add(new WaterBodyData
                    {
                        Name = CleanWaterBodyName(name),
                        County = CleanCountyName(county),
                        WaterType = DetermineWaterType(name),
                        State = "Minnesota",
                        IsConnectedSystem = false,
                        ConnectedWaters = new List<string>(),
                        AlternateNames = new List<string>()
                    });
                }
            }
            
            _logger.LogInformation("Fallback extraction found {Count} water bodies in chunk {ChunkNumber}", 
                waterBodies.Count, chunkNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback extraction failed for chunk {ChunkNumber}", chunkNumber);
        }
        
        return waterBodies;
    }

    private bool IsValidWaterBodyEntry(string name, string county)
    {
        return name.Length > 3 && 
               !name.Contains("SPECIES", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("SEASON", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("ZONE", StringComparison.OrdinalIgnoreCase) &&
               county.Length > 2 &&
               county.Length < 30;
    }

    private string DetermineWaterType(string name)
    {
        if (name.Contains("RIVER", StringComparison.OrdinalIgnoreCase))
            return "river";
        if (name.Contains("STREAM", StringComparison.OrdinalIgnoreCase) || name.Contains("CREEK", StringComparison.OrdinalIgnoreCase))
            return "stream";
        if (name.Contains("POND", StringComparison.OrdinalIgnoreCase))
            return "pond";
        if (name.Contains("RESERVOIR", StringComparison.OrdinalIgnoreCase))
            return "reservoir";
        return "lake";
    }

    private string CleanWaterBodyName(string name)
    {
        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }
        return string.Join(" ", words);
    }

    private string CleanCountyName(string county)
    {
        county = county.Trim();
        if (county.EndsWith("County", StringComparison.OrdinalIgnoreCase))
        {
            county = county[..^6].Trim();
        }
        
        var words = county.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }
        return string.Join(" ", words);
    }

    private async Task<ComprehensiveExtractionResult<List<WaterBodyRegulationData>>> ExtractRegulationsFromChunk(
        string textChunk, 
        int chunkNumber, 
        int totalChunks)
    {
        var result = new ComprehensiveExtractionResult<List<WaterBodyRegulationData>>();
        
        try
        {
            var prompt = BuildRegulationExtractionPrompt(textChunk, chunkNumber, totalChunks);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert at analyzing fishing regulation documents and extracting detailed regulation data. 
Your task is to systematically extract ALL regulations for ALL water bodies mentioned in this text chunk.
You must respond with valid JSON format only."),
                new UserChatMessage(prompt)
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 8000,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            var jsonContent = response.Value.Content[0].Text;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new RegulationTypeConverter() }
            };
            
            result = JsonSerializer.Deserialize<ComprehensiveExtractionResult<List<WaterBodyRegulationData>>>(jsonContent, options) 
                ?? new ComprehensiveExtractionResult<List<WaterBodyRegulationData>>();
            
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting regulations from chunk {ChunkNumber}", chunkNumber);
            result.ErrorMessage = ex.Message;
            result.IsSuccess = false;
        }

        return result;
    }

    private string BuildCountyExtractionPrompt(string regulationsText)
    {
        var textPreview = regulationsText.Length > 25000 
            ? regulationsText[..25000] + "\n\n[...text truncated for processing...]"
            : regulationsText;
            
        return $@"Analyze this fishing regulations document and extract ALL counties mentioned. Return as clean, standardized JSON list.

DOCUMENT TEXT:
{textPreview}

RETURN FORMAT (JSON):
{{""isSuccess"": true, ""data"": [{{""name"": ""County Name"", ""state"": ""Minnesota"", ""fipsCode"": null}}], ""errorMessage"": """", ""totalExtracted"": 0, ""processingWarnings"": []}}

Extract every unique county mentioned. Return valid JSON only.";
    }

    private string BuildWaterBodyExtractionPrompt(string regulationsText, int chunkNumber = 1, int totalChunks = 1)
    {
        var chunkInfo = totalChunks > 1 ? $" (Chunk {chunkNumber} of {totalChunks})" : "";
        
        return $@"Extract ALL water bodies from this text{chunkInfo}. ENSURE COMPLETE JSON.

TEXT:
{regulationsText}

CRITICAL: Extract every ""LAKE NAME (County)"" pattern. If approaching token limits, ensure JSON is properly closed.

RETURN FORMAT (JSON):
{{""isSuccess"": true, ""data"": [{{""name"": ""Lake Name"", ""waterType"": ""lake"", ""county"": ""County"", ""state"": ""Minnesota"", ""isConnectedSystem"": false, ""connectedWaters"": [], ""alternateNames"": []}}], ""errorMessage"": """", ""totalExtracted"": 0, ""processingWarnings"": []}}

Extract every water body. Return complete, valid JSON only.";
    }

    private string BuildFishSpeciesExtractionPrompt(string regulationsText)
    {
        var textPreview = regulationsText.Length > 25000 ? regulationsText[..25000] + "\n\n[...truncated...]" : regulationsText;
        return $@"Extract ALL fish species from document. Return as JSON.

DOCUMENT: {textPreview}

RETURN FORMAT: {{""isSuccess"": true, ""data"": [{{""commonName"": ""Species Name"", ""scientificName"": null, ""speciesCode"": null, ""alternateNames"": [], ""isGameFish"": true, ""isProtected"": false}}], ""errorMessage"": """", ""totalExtracted"": 0, ""processingWarnings"": []}}

Return valid JSON only.";
    }

    private string BuildRegulationExtractionPrompt(string regulationsText, int chunkNumber = 1, int totalChunks = 1)
    {
        var chunkInfo = totalChunks > 1 ? $" (Chunk {chunkNumber} of {totalChunks})" : "";
        return $@"Extract ALL regulations from this text{chunkInfo}. Return as JSON.

TEXT: {regulationsText}

RETURN FORMAT: {{""isSuccess"": true, ""data"": [{{""waterBodyName"": ""Lake Name"", ""county"": ""County"", ""waterType"": ""lake"", ""regulations"": [{{""species"": ""Species"", ""regulationType"": ""DailyLimit"", ""dailyLimit"": 6, ""notes"": """"}}], ""generalNotes"": """"}}], ""errorMessage"": """", ""totalExtracted"": 0, ""processingWarnings"": []}}

Return valid JSON only.";
    }
}

// Data classes...
public class ComprehensiveExtractionResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalExtracted { get; set; }
    public List<string> ProcessingWarnings { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
}

public class CountyData
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "Minnesota";
    public string? FipsCode { get; set; }
}

public class WaterBodyData
{
    public string Name { get; set; } = string.Empty;
    public string WaterType { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string State { get; set; } = "Minnesota";
    public bool IsConnectedSystem { get; set; }
    public List<string> ConnectedWaters { get; set; } = new();
    public List<string> AlternateNames { get; set; } = new();
}

public class FishSpeciesData
{
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? SpeciesCode { get; set; }
    public List<string> AlternateNames { get; set; } = new();
    public bool IsGameFish { get; set; }
    public bool IsProtected { get; set; }
}

public class WaterBodyRegulationData
{
    public string WaterBodyName { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string WaterType { get; set; } = string.Empty;
    public List<DetailedRegulationData> Regulations { get; set; } = new();
    public string? GeneralNotes { get; set; }
}

public class DetailedRegulationData
{
    public string Species { get; set; } = string.Empty;
    public AiRegulationType RegulationType { get; set; }
    public int? DailyLimit { get; set; }
    public int? PossessionLimit { get; set; }
    public decimal? MinimumSizeInches { get; set; }
    public decimal? MaximumSizeInches { get; set; }
    public decimal? ProtectedSlotMinInches { get; set; }
    public decimal? ProtectedSlotMaxInches { get; set; }
    public int? ProtectedSlotExceptions { get; set; }
    public int? SeasonStartMonth { get; set; }
    public int? SeasonStartDay { get; set; }
    public int? SeasonEndMonth { get; set; }
    public int? SeasonEndDay { get; set; }
    public bool IsCatchAndRelease { get; set; }
    public List<string> SpecialRegulations { get; set; } = new();
    public string? Notes { get; set; }
}