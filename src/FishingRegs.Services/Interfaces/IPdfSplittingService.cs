using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for splitting large PDF documents into smaller chunks for processing
/// </summary>
public interface IPdfSplittingService
{
    /// <summary>
    /// Splits a PDF into smaller chunks if needed based on size limits
    /// </summary>
    /// <param name="pdfStream">The PDF stream to split</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="maxSizeKb">Maximum size per chunk in KB (default: 4MB)</param>
    /// <returns>Result containing chunks or original document if splitting not needed</returns>
    Task<PdfSplitResult> SplitPdfAsync(Stream pdfStream, string fileName, int maxSizeKb = 4000);
    
    /// <summary>
    /// Processes a PDF by intelligently splitting it if needed and merging results
    /// </summary>
    /// <param name="pdfStream">The PDF stream to process</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">Content type of the document</param>
    /// <returns>Merged analysis result from all chunks</returns>
    Task<DocumentAnalysisResult> ProcessSplitPdfAsync(Stream pdfStream, string fileName, string contentType);
    
    /// <summary>
    /// Merges multiple analysis results from chunks into a single result
    /// </summary>
    /// <param name="results">Collection of chunk analysis results</param>
    /// <returns>Merged analysis result</returns>
    Task<DocumentAnalysisResult> MergeAnalysisResults(IEnumerable<DocumentAnalysisResult> results);
}
