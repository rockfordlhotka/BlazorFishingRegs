using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for chunking extracted text into manageable pieces for processing
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;
    private const int DEFAULT_MAX_CHUNK_SIZE = 15000; // Characters per chunk (safe for most AI services)
    private const int MIN_CHUNK_SIZE = 1000; // Minimum chunk size
    private const int OVERLAP_SIZE = 200; // Overlap between chunks to maintain context

    public TextChunkingService(ILogger<TextChunkingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Chunks text into manageable pieces for processing
    /// </summary>
    /// <param name="text">Text to chunk</param>
    /// <param name="maxChunkSize">Maximum size per chunk in characters</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks</param>
    /// <returns>Text chunking result</returns>
    public TextChunkingResult ChunkText(string text, int maxChunkSize = 4000, int overlapSize = 200)
    {
        try
        {
            _logger.LogInformation("Chunking text of {Length} characters into chunks of max {MaxSize} characters", 
                text.Length, maxChunkSize);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new TextChunkingResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Text is empty or null"
                };
            }

            if (text.Length <= maxChunkSize)
            {
                _logger.LogInformation("Text fits in single chunk, no chunking needed");
                return new TextChunkingResult
                {
                    IsSuccess = true,
                    OriginalTextLength = text.Length,
                    Chunks = new List<TextChunk>
                    {
                        new TextChunk
                        {
                            ChunkNumber = 1,
                            Content = text,
                            EstimatedPageStart = 1,
                            EstimatedPageEnd = Math.Max(1, text.Length / 2000),
                            ContainsFishingContent = ContainsFishingContent(text)
                        }
                    }
                };
            }

            var chunks = new List<TextChunk>();
            var chunkNumber = 1;
            var position = 0;

            while (position < text.Length)
            {
                var chunkEnd = Math.Min(position + maxChunkSize, text.Length);
                
                // Try to find a good break point (paragraph, sentence, or word boundary)
                if (chunkEnd < text.Length)
                {
                    chunkEnd = FindBestBreakPoint(text, position, chunkEnd);
                }

                var chunkContent = text.Substring(position, chunkEnd - position);
                
                // Add overlap from previous chunk for context (except for first chunk)
                if (position > 0 && overlapSize > 0)
                {
                    var overlapStart = Math.Max(0, position - overlapSize);
                    var overlapContent = text.Substring(overlapStart, position - overlapStart);
                    chunkContent = overlapContent + chunkContent;
                }

                var chunk = new TextChunk
                {
                    ChunkNumber = chunkNumber,
                    Content = chunkContent,
                    EstimatedPageStart = (position / 2000) + 1,
                    EstimatedPageEnd = (chunkEnd / 2000) + 1,
                    ContainsFishingContent = ContainsFishingContent(chunkContent)
                };

                chunks.Add(chunk);
                
                _logger.LogDebug("Created chunk {ChunkNumber}: {Length} characters, pages {StartPage}-{EndPage}, fishing content: {HasFishing}",
                    chunkNumber, chunk.CharacterCount, chunk.EstimatedPageStart, chunk.EstimatedPageEnd, chunk.ContainsFishingContent);

                position = chunkEnd;
                chunkNumber++;
            }

            _logger.LogInformation("Successfully chunked text into {ChunkCount} chunks", chunks.Count);

            return new TextChunkingResult
            {
                IsSuccess = true,
                OriginalTextLength = text.Length,
                Chunks = chunks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error chunking text");
            return new TextChunkingResult
            {
                IsSuccess = false,
                ErrorMessage = $"Text chunking failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Finds the best break point for chunking (paragraph, sentence, or word boundary)
    /// </summary>
    private int FindBestBreakPoint(string text, int start, int maxEnd)
    {
        // Look for paragraph break (double newline)
        var paragraphBreak = text.LastIndexOf("\n\n", maxEnd, maxEnd - start);
        if (paragraphBreak > start && paragraphBreak > start + MIN_CHUNK_SIZE)
        {
            return paragraphBreak + 2; // Include the newlines
        }

        // Look for sentence break (period followed by space or newline)
        var sentencePattern = @"[.!?]\s";
        var matches = Regex.Matches(text.Substring(start, maxEnd - start), sentencePattern);
        if (matches.Count > 0)
        {
            var lastMatch = matches[matches.Count - 1];
            var sentenceBreak = start + lastMatch.Index + lastMatch.Length;
            if (sentenceBreak > start + MIN_CHUNK_SIZE)
            {
                return sentenceBreak;
            }
        }

        // Look for line break
        var lineBreak = text.LastIndexOf('\n', maxEnd, maxEnd - start);
        if (lineBreak > start && lineBreak > start + MIN_CHUNK_SIZE)
        {
            return lineBreak + 1;
        }

        // Look for word boundary (space)
        var wordBreak = text.LastIndexOf(' ', maxEnd, Math.Min(200, maxEnd - start));
        if (wordBreak > start && wordBreak > start + MIN_CHUNK_SIZE)
        {
            return wordBreak + 1;
        }

        // If no good break point found, use the maximum end
        return maxEnd;
    }

    /// <summary>
    /// Checks if text contains fishing regulation content
    /// </summary>
    private bool ContainsFishingContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lowerText = text.ToLowerInvariant();
        
        var fishingKeywords = new[]
        {
            "fishing", "fish", "angling", "angler",
            "lake", "river", "stream", "water",
            "regulation", "rule", "limit", "restriction",
            "season", "license", "permit",
            "bass", "trout", "walleye", "pike", "salmon",
            "daily limit", "possession", "size limit",
            "closed season", "open season"
        };

        return fishingKeywords.Any(keyword => lowerText.Contains(keyword));
    }

    /// <summary>
    /// Filters chunks to only include those with fishing content
    /// </summary>
    public TextChunkingResult FilterFishingChunks(TextChunkingResult result)
    {
        if (!result.IsSuccess)
            return result;

        var fishingChunks = result.Chunks.Where(c => c.ContainsFishingContent).ToList();
        
        // Renumber the chunks
        for (int i = 0; i < fishingChunks.Count; i++)
        {
            fishingChunks[i].ChunkNumber = i + 1;
        }

        _logger.LogInformation("Filtered {OriginalCount} chunks to {FilteredCount} chunks with fishing content",
            result.Chunks.Count, fishingChunks.Count);

        return new TextChunkingResult
        {
            IsSuccess = true,
            OriginalTextLength = result.OriginalTextLength,
            Chunks = fishingChunks
        };
    }

    /// <summary>
    /// Chunks text while trying to preserve logical boundaries (sentences, paragraphs)
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="maxChunkSize">Maximum size per chunk in characters</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks</param>
    /// <returns>Text chunking result with organized chunks</returns>
    public TextChunkingResult ChunkTextIntelligently(string text, int maxChunkSize = 4000, int overlapSize = 200)
    {
        // For now, delegate to the basic chunking method
        // TODO: Implement intelligent boundary-aware chunking
        return ChunkText(text, maxChunkSize, overlapSize);
    }

    /// <summary>
    /// Analyzes a chunk to determine if it contains fishing-related content
    /// </summary>
    /// <param name="chunk">Text chunk to analyze</param>
    /// <returns>True if the chunk contains fishing content</returns>
    public bool ContainsFishingContent(TextChunk chunk)
    {
        return chunk.ContainsFishingContent;
    }

    /// <summary>
    /// Validates text chunking result for quality and completeness
    /// </summary>
    /// <param name="originalText">Original text before chunking</param>
    /// <param name="chunkingResult">Result to validate</param>
    /// <returns>Validation result with details</returns>
    public TextChunkValidationResult ValidateChunking(string originalText, TextChunkingResult chunkingResult)
    {
        try
        {
            var result = new TextChunkValidationResult();
            var issues = new List<string>();

            if (!chunkingResult.IsSuccess)
            {
                issues.Add("Chunking operation failed");
                result.IsValid = false;
                result.Issues = issues;
                return result;
            }

            if (chunkingResult.Chunks == null || !chunkingResult.Chunks.Any())
            {
                issues.Add("No chunks generated");
                result.IsValid = false;
                result.Issues = issues;
                return result;
            }

            // Calculate coverage
            var totalChunkChars = chunkingResult.TotalChunkCharacters;
            result.CoveragePercentage = originalText.Length > 0 ? 
                (double)totalChunkChars / originalText.Length * 100 : 0;

            if (result.CoveragePercentage < 95)
            {
                issues.Add($"Low coverage: {result.CoveragePercentage:F1}% of original text");
            }

            // Check fishing content
            result.FishingContentChunks = chunkingResult.Chunks.Count(c => c.ContainsFishingContent);
            result.FishingContentPercentage = chunkingResult.Chunks.Count > 0 ?
                (double)result.FishingContentChunks / chunkingResult.Chunks.Count * 100 : 0;

            if (result.FishingContentPercentage < 10)
            {
                issues.Add($"Low fishing content: {result.FishingContentPercentage:F1}% of chunks");
            }

            // Calculate quality score
            var coverageScore = Math.Min(1.0, result.CoveragePercentage / 100);
            var fishingScore = Math.Min(1.0, result.FishingContentPercentage / 50); // 50% is considered good
            result.QualityScore = (coverageScore * 0.7) + (fishingScore * 0.3);

            result.IsValid = issues.Count == 0 && result.QualityScore > 0.5;
            result.Issues = issues;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating text chunking");
            return new TextChunkValidationResult
            {
                IsValid = false,
                Issues = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }
}
