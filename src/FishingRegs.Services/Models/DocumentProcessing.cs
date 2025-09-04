using System;

namespace FishingRegs.Services.Models;

/// <summary>
/// Status of document processing
/// </summary>
public enum DocumentProcessingStatus
{
    Started,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Represents a document processed for fishing regulations
/// </summary>
public class ProcessingDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string BlobUrl { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Started;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public FishingRegulationData? FishingRegulationData { get; set; }
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
/// Configuration for text file validation
/// </summary>
public class TextValidationOptions
{
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024; // 10MB for text files
    public string[] AllowedContentTypes { get; init; } = { "text/plain", "text/txt", "application/text" };
    public string[] RequiredKeywords { get; init; } = { "fishing", "regulation", "lake" };
    public int MinimumCharacterLength { get; init; } = 100;
}
