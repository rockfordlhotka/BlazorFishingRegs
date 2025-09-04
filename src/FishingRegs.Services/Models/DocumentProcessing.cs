using System;

namespace FishingRegs.Services.Models;

/// <summary>
/// Status of document processing
/// </summary>
public enum DocumentProcessingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Represents a document uploaded for processing
/// </summary>
public class ProcessingDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string BlobUrl { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending;
    public DateTime UploadedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DocumentAnalysisResult? AnalysisResult { get; set; }
}

/// <summary>
/// Result of blob storage upload operation
/// </summary>
public class BlobUploadResult
{
    public string BlobName { get; init; } = string.Empty;
    public string BlobUrl { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration for PDF validation
/// </summary>
public class PdfValidationOptions
{
    public long MaxFileSizeBytes { get; init; } = 50 * 1024 * 1024; // 50MB
    public string[] AllowedContentTypes { get; init; } = { "application/pdf" };
    public string[] RequiredKeywords { get; init; } = { "fishing", "regulation", "lake" };
}
