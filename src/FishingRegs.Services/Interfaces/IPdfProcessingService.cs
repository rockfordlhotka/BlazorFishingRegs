using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// PDF processing service for fishing regulations documents
/// </summary>
public interface IPdfProcessingService
{
    /// <summary>
    /// Validates a PDF file for fishing regulations processing
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="contentType">Content type</param>
    /// <param name="fileSize">File size in bytes</param>
    /// <param name="stream">File stream for content validation</param>
    /// <returns>True if valid</returns>
    Task<bool> ValidatePdfAsync(
        string fileName, 
        string contentType, 
        long fileSize, 
        Stream stream);

    /// <summary>
    /// Processes a PDF document end-to-end
    /// </summary>
    /// <param name="stream">PDF document stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">Content type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing document with analysis results</returns>
    Task<ProcessingDocument> ProcessPdfAsync(
        Stream stream,
        string fileName,
        string contentType,
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
    /// Extracts fishing regulation data from analysis results
    /// </summary>
    /// <param name="analysisResult">Document analysis result</param>
    /// <returns>Extracted fishing regulation data</returns>
    Task<FishingRegulationData> ExtractFishingRegulationDataAsync(
        DocumentAnalysisResult analysisResult);
}

/// <summary>
/// Extracted fishing regulation data
/// </summary>
public class FishingRegulationData
{
    public List<LakeRegulation> Lakes { get; init; } = new();
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
    public double OverallConfidence { get; init; }
}

/// <summary>
/// Fishing regulation for a specific lake
/// </summary>
public class LakeRegulation
{
    public string LakeName { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string County { get; init; } = string.Empty;
    public List<SpeciesRegulation> Species { get; init; } = new();
    public List<string> SpecialRegulations { get; init; } = new();
    public double Confidence { get; init; }
}

/// <summary>
/// Regulation for a specific fish species
/// </summary>
public class SpeciesRegulation
{
    public string SpeciesName { get; init; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public int? BagLimit { get; set; }
    public string? SizeLimit { get; set; }
    public List<string> Restrictions { get; init; } = new();
    public double Confidence { get; init; }
}
