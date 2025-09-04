using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents an uploaded regulation document (text file)
/// </summary>
[Table("regulation_documents")]
public class RegulationDocument
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("original_file_name")]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    [Column("blob_storage_url")]
    public string BlobStorageUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("blob_container")]
    public string BlobContainer { get; set; } = string.Empty;

    [Required]
    [Column("state_id")]
    public int StateId { get; set; }

    [Required]
    [Column("regulation_year")]
    public int RegulationYear { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("document_type")]
    public string DocumentType { get; set; } = "fishing_regulations";

    [Required]
    [MaxLength(50)]
    [Column("upload_source")]
    public string UploadSource { get; set; } = "manual";

    [Required]
    [MaxLength(20)]
    [Column("processing_status")]
    public string ProcessingStatus { get; set; } = "pending";

    [Column("processing_started_at")]
    public DateTimeOffset? ProcessingStartedAt { get; set; }

    [Column("processing_completed_at")]
    public DateTimeOffset? ProcessingCompletedAt { get; set; }

    [Column("processing_error")]
    public string? ProcessingError { get; set; }

    [Column("extracted_data")]
    public string? ExtractedDataJson { get; set; } // JSONB stored as string

    [Column("confidence_score")]
    [Range(0.0, 1.0)]
    public decimal? ConfidenceScore { get; set; }

    [MaxLength(255)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("StateId")]
    public virtual State State { get; set; } = null!;

    public virtual ICollection<FishingRegulation> FishingRegulations { get; set; } = new List<FishingRegulation>();

    // Computed properties
    [NotMapped]
    public bool IsProcessing => ProcessingStatus == "processing";

    [NotMapped]
    public bool IsCompleted => ProcessingStatus == "completed";

    [NotMapped]
    public bool HasError => ProcessingStatus == "failed";

    [NotMapped]
    public TimeSpan? ProcessingDuration
    {
        get
        {
            if (ProcessingStartedAt.HasValue && ProcessingCompletedAt.HasValue)
                return ProcessingCompletedAt.Value - ProcessingStartedAt.Value;
            return null;
        }
    }
}
