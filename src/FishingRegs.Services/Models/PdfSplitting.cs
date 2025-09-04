namespace FishingRegs.Services.Models;

/// <summary>
/// Result of PDF splitting operation
/// </summary>
public class PdfSplitResult
{
    /// <summary>
    /// Whether the splitting operation was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// List of PDF chunks created from the original document
    /// </summary>
    public List<PdfChunk> Chunks { get; set; } = new();
    
    /// <summary>
    /// Error message if splitting failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the document required splitting (false if it was within size limits)
    /// </summary>
    public bool RequiredSplitting { get; set; }
}

/// <summary>
/// Represents a chunk of a split PDF document
/// </summary>
public class PdfChunk
{
    /// <summary>
    /// Sequential number of this chunk (1-based)
    /// </summary>
    public int ChunkNumber { get; set; }
    
    /// <summary>
    /// Binary data of the PDF chunk
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Generated filename for this chunk
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Starting page number in the original document (1-based)
    /// </summary>
    public int PageStart { get; set; }
    
    /// <summary>
    /// Ending page number in the original document (1-based)
    /// </summary>
    public int PageEnd { get; set; }
    
    /// <summary>
    /// Size of this chunk in bytes
    /// </summary>
    public long SizeBytes { get; set; }
}
