using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for chunking text into manageable pieces for processing
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Chunks text into smaller segments based on size and content
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="maxChunkSize">Maximum size per chunk in characters (default: 4000)</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks (default: 200)</param>
    /// <returns>Text chunking result with organized chunks</returns>
    TextChunkingResult ChunkText(string text, int maxChunkSize = 4000, int overlapSize = 200);

    /// <summary>
    /// Chunks text while trying to preserve logical boundaries (sentences, paragraphs)
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="maxChunkSize">Maximum size per chunk in characters (default: 4000)</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks (default: 200)</param>
    /// <returns>Text chunking result with organized chunks</returns>
    TextChunkingResult ChunkTextIntelligently(string text, int maxChunkSize = 4000, int overlapSize = 200);

    /// <summary>
    /// Filters chunks to focus on fishing-related content
    /// </summary>
    /// <param name="chunkingResult">Original chunking result</param>
    /// <returns>Filtered result with only fishing-related chunks</returns>
    TextChunkingResult FilterFishingChunks(TextChunkingResult chunkingResult);

    /// <summary>
    /// Analyzes a chunk to determine if it contains fishing-related content
    /// </summary>
    /// <param name="chunk">Text chunk to analyze</param>
    /// <returns>True if the chunk contains fishing content</returns>
    bool ContainsFishingContent(TextChunk chunk);

    /// <summary>
    /// Validates text chunking result for quality and completeness
    /// </summary>
    /// <param name="originalText">Original text before chunking</param>
    /// <param name="chunkingResult">Result to validate</param>
    /// <returns>Validation result with details</returns>
    TextChunkValidationResult ValidateChunking(string originalText, TextChunkingResult chunkingResult);
}
