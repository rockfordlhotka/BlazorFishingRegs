using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for extracting text from PDF documents using various methods
/// </summary>
public interface IPdfTextExtractionService
{
    /// <summary>
    /// Extracts text from a PDF using the best available method
    /// </summary>
    /// <param name="pdfStream">PDF stream</param>
    /// <param name="fileName">Original filename</param>
    /// <returns>Text extraction result</returns>
    Task<TextExtractionResult> ExtractTextAsync(Stream pdfStream, string fileName);
    
    /// <summary>
    /// Extracts text using pdftotext CLI tool (requires poppler-utils)
    /// </summary>
    /// <param name="pdfStream">PDF stream</param>
    /// <param name="fileName">Original filename</param>
    /// <returns>Text extraction result</returns>
    Task<TextExtractionResult> ExtractTextWithPdfToTextAsync(Stream pdfStream, string fileName);
    
    /// <summary>
    /// Extracts text using .NET libraries (fallback method)
    /// </summary>
    /// <param name="pdfStream">PDF stream</param>
    /// <param name="fileName">Original filename</param>
    /// <returns>Text extraction result</returns>
    Task<TextExtractionResult> ExtractTextWithLibraryAsync(Stream pdfStream, string fileName);
    
    /// <summary>
    /// Checks if pdftotext CLI tool is available on the system
    /// </summary>
    /// <returns>True if pdftotext is available</returns>
    Task<bool> IsPdfToTextAvailableAsync();
}
