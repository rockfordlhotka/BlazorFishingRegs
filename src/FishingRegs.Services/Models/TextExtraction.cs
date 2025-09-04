namespace FishingRegs.Services.Models;

/// <summary>
/// Result of text extraction from PDF
/// </summary>
public class TextExtractionResult
{
    /// <summary>
    /// Whether text extraction was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Extracted text content
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;
    
    /// <summary>
    /// Method used for extraction (pdftotext, library, etc.)
    /// </summary>
    public string ExtractionMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Time taken for extraction
    /// </summary>
    public TimeSpan ExtractionTime { get; set; }
    
    /// <summary>
    /// Number of characters extracted
    /// </summary>
    public int CharacterCount => ExtractedText.Length;
    
    /// <summary>
    /// Estimated number of pages based on character count
    /// </summary>
    public int EstimatedPageCount => Math.Max(1, CharacterCount / 2000); // ~2000 chars per page
}

/// <summary>
/// Represents a chunk of extracted text
/// </summary>
public class TextChunk
{
    /// <summary>
    /// Sequential number of this chunk (1-based)
    /// </summary>
    public int ChunkNumber { get; set; }
    
    /// <summary>
    /// Text content of this chunk
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Size of this chunk in characters
    /// </summary>
    public int CharacterCount => Content.Length;
    
    /// <summary>
    /// Estimated starting page (based on character position)
    /// </summary>
    public int EstimatedPageStart { get; set; }
    
    /// <summary>
    /// Estimated ending page (based on character position)
    /// </summary>
    public int EstimatedPageEnd { get; set; }
    
    /// <summary>
    /// Whether this chunk contains fishing regulation content
    /// </summary>
    public bool ContainsFishingContent { get; set; }
}

/// <summary>
/// Result of text chunking operation
/// </summary>
public class TextChunkingResult
{
    /// <summary>
    /// Whether chunking was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// List of text chunks
    /// </summary>
    public List<TextChunk> Chunks { get; set; } = new();
    
    /// <summary>
    /// Error message if chunking failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Original text length
    /// </summary>
    public int OriginalTextLength { get; set; }
    
    /// <summary>
    /// Total characters across all chunks
    /// </summary>
    public int TotalChunkCharacters => Chunks.Sum(c => c.CharacterCount);
}

/// <summary>
/// Result of text chunk validation
/// </summary>
public class TextChunkValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// List of validation issues found
    /// </summary>
    public List<string> Issues { get; set; } = new();
    
    /// <summary>
    /// Percentage of original text covered by chunks
    /// </summary>
    public double CoveragePercentage { get; set; }
    
    /// <summary>
    /// Number of chunks containing fishing content
    /// </summary>
    public int FishingContentChunks { get; set; }
    
    /// <summary>
    /// Percentage of chunks containing fishing content
    /// </summary>
    public double FishingContentPercentage { get; set; }
    
    /// <summary>
    /// Quality score from 0.0 to 1.0
    /// </summary>
    public double QualityScore { get; set; }
}
