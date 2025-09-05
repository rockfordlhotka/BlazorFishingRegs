using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents fishing regulations for a specific water body and species
/// </summary>
[Table("fishing_regulations")]
public class FishingRegulation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("water_body_id")]
    public int WaterBodyId { get; set; }

    [Required]
    [Column("species_id")]
    public int SpeciesId { get; set; }

    [Required]
    [Column("regulation_year")]
    public int RegulationYear { get; set; }

    [Column("regulation_document_id")]
    public Guid? SourceDocumentId { get; set; }

    [Required]
    [Column("effective_date")]
    public DateOnly EffectiveDate { get; set; }

    [Column("expiration_date")]
    public DateOnly? ExpirationDate { get; set; }

    // Season Information
    [Column("season_open_date")]
    public DateOnly? SeasonOpenDate { get; set; }

    [Column("season_close_date")]
    public DateOnly? SeasonCloseDate { get; set; }

    [Column("is_year_round")]
    public bool IsYearRound { get; set; } = false;

    // Bag Limits
    [Column("daily_limit")]
    public int? DailyLimit { get; set; }

    [Column("possession_limit")]
    public int? PossessionLimit { get; set; }

    [Column("bag_limit_notes")]
    public string? BagLimitNotes { get; set; }

    // Size Limits (in inches)
    [Column("minimum_size_inches")]
    public decimal? MinimumSizeInches { get; set; }

    [Column("maximum_size_inches")]
    public decimal? MaximumSizeInches { get; set; }

    [Column("protected_slot_min_inches")]
    public decimal? ProtectedSlotMinInches { get; set; }

    [Column("protected_slot_max_inches")]
    public decimal? ProtectedSlotMaxInches { get; set; }

    [Column("protected_slot_exceptions")]
    public int? ProtectedSlotExceptions { get; set; }

    [Column("size_limit_notes")]
    public string? SizeLimitNotes { get; set; }

    // Special Regulations (stored as arrays in PostgreSQL)
    [Column("special_regulations")]
    public List<string> SpecialRegulations { get; set; } = new();

    [Column("bait_restrictions")]
    public string? BaitRestrictions { get; set; }

    [Column("gear_restrictions")]
    public string? GearRestrictions { get; set; }

    [Column("method_restrictions")]
    public string? MethodRestrictions { get; set; }

    // License Requirements
    [Column("requires_special_stamp")]
    public bool RequiresSpecialStamp { get; set; } = false;

    [Column("required_stamps")]
    public List<string> RequiredStamps { get; set; } = new();

    // Metadata
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("confidence_score")]
    [Range(0.0, 1.0)]
    public decimal? ConfidenceScore { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("review_status")]
    public string ReviewStatus { get; set; } = "pending";

    [MaxLength(255)]
    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTimeOffset? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("WaterBodyId")]
    public virtual WaterBody WaterBody { get; set; } = null!;

    [ForeignKey("SpeciesId")]
    public virtual FishSpecies Species { get; set; } = null!;

    [ForeignKey("SourceDocumentId")]
    public virtual RegulationDocument? SourceDocument { get; set; }

    // Computed properties
    [NotMapped]
    public bool IsCurrentlyOpen
    {
        get
        {
            if (IsYearRound) return true;
            if (!SeasonOpenDate.HasValue) return false;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var currentYear = today.Year;

            // Handle seasons that cross year boundaries
            var openDate = new DateOnly(currentYear, SeasonOpenDate.Value.Month, SeasonOpenDate.Value.Day);
            var closeDate = SeasonCloseDate.HasValue 
                ? new DateOnly(currentYear, SeasonCloseDate.Value.Month, SeasonCloseDate.Value.Day)
                : new DateOnly(currentYear, 12, 31);

            if (openDate <= closeDate)
            {
                // Normal season within calendar year
                return today >= openDate && today <= closeDate;
            }
            else
            {
                // Season crosses year boundary (e.g., Nov 1 - Mar 15)
                return today >= openDate || today <= closeDate;
            }
        }
    }

    [NotMapped]
    public bool IsExpired => ExpirationDate.HasValue && DateOnly.FromDateTime(DateTime.Today) > ExpirationDate.Value;

    [NotMapped]
    public bool NeedsReview => ReviewStatus == "pending" || ReviewStatus == "needs_revision";

    [NotMapped]
    public bool IsApproved => ReviewStatus == "approved";

    [NotMapped]
    public bool HasProtectedSlot => ProtectedSlotMinInches.HasValue && ProtectedSlotMaxInches.HasValue;

    [NotMapped]
    public string DisplaySeason
    {
        get
        {
            if (IsYearRound) return "Year Round";
            if (!SeasonOpenDate.HasValue) return "Season dates not specified";

            var openStr = SeasonOpenDate.Value.ToString("MMM d");
            var closeStr = SeasonCloseDate?.ToString("MMM d") ?? "Year End";
            return $"{openStr} - {closeStr}";
        }
    }

    [NotMapped]
    public string DisplayBagLimit
    {
        get
        {
            if (!DailyLimit.HasValue) return "No limit specified";
            
            var daily = $"{DailyLimit} daily";
            if (PossessionLimit.HasValue && PossessionLimit != DailyLimit)
                daily += $", {PossessionLimit} possession";
            
            return daily;
        }
    }

    [NotMapped]
    public string DisplaySizeLimit
    {
        get
        {
            var parts = new List<string>();

            if (MinimumSizeInches.HasValue)
                parts.Add($"Min: {MinimumSizeInches}\"");

            if (MaximumSizeInches.HasValue)
                parts.Add($"Max: {MaximumSizeInches}\"");

            if (HasProtectedSlot)
            {
                var slot = $"Protected slot: {ProtectedSlotMinInches}\"-{ProtectedSlotMaxInches}\"";
                if (ProtectedSlotExceptions.HasValue && ProtectedSlotExceptions > 0)
                    slot += $" ({ProtectedSlotExceptions} allowed)";
                parts.Add(slot);
            }

            return parts.Any() ? string.Join(", ", parts) : "No size limits";
        }
    }
}
