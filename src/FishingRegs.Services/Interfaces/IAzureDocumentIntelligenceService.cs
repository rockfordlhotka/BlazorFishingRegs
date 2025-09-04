using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Azure AI Document Intelligence service for PDF analysis
/// </summary>
public interface IAzureDocumentIntelligenceService
{
    /// <summary>
    /// Analyzes a document from a URL using Azure Document Intelligence
    /// </summary>
    /// <param name="documentUrl">URL of the document to analyze</param>
    /// <param name="modelId">Model ID to use for analysis (default: prebuilt-document)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document analysis result</returns>
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        string documentUrl, 
        string modelId = "prebuilt-document",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a document from a stream using Azure Document Intelligence
    /// </summary>
    /// <param name="documentStream">Stream containing the document</param>
    /// <param name="contentType">Content type of the document</param>
    /// <param name="modelId">Model ID to use for analysis (default: prebuilt-document)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document analysis result</returns>
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Stream documentStream,
        string contentType,
        string modelId = "prebuilt-document",
        CancellationToken cancellationToken = default);
}
