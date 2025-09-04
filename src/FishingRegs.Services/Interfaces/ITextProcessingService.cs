using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Text processing service for fishing regulations documents
/// </summary>
public interface ITextProcessingService
{
    /// <summary>
    /// Processes text file content and extracts fishing regulation data
    /// </summary>
    /// <param name="textContent">Raw text content from file</param>
    /// <param name="fileName">Name of the source file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing document with analysis results</returns>
    Task<ProcessingDocument> ProcessTextAsync(
        string textContent,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing status for a document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing document</returns>
    Task<ProcessingDocument?> GetProcessingStatusAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts fishing regulation data from text using AI
    /// </summary>
    /// <param name="textContent">Text content to analyze</param>
    /// <param name="fileName">Source file name</param>
    /// <returns>Extracted fishing regulation data</returns>
    Task<FishingRegulationData> ExtractFishingRegulationDataAsync(
        string textContent,
        string fileName);

    /// <summary>
    /// Validates text content for processing
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="textContent">Text content to validate</param>
    /// <returns>True if content is valid for processing</returns>
    Task<bool> ValidateTextAsync(string fileName, string textContent);
}
