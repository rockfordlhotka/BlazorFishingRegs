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
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

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
    [MaxLength(50)]
    [Column("regulation_type")]
    public string RegulationType { get; set; } = "general";

    [Required]
    [Column("effective_date")]
    public DateOnly EffectiveDate { get; set; }

    [Column("expiration_date")]
    public DateOnly? ExpirationDate { get; set; }

    // Season Information
    [Column("season_start_month")]
    public int? SeasonStartMonth { get; set; }

    [Column("season_start_day")]
    public int? SeasonStartDay { get; set; }

    [Column("season_end_month")]
    public int? SeasonEndMonth { get; set; }

    [Column("season_end_day")]
    public int? SeasonEndDay { get; set; }

    [Column("is_catch_and_release")]
    public bool IsCatchAndRelease { get; set; } = false;

    // Bag Limits
    [Column("daily_limit")]
    public int? DailyLimit { get; set; }

    [Column("possession_limit")]
    public int? PossessionLimit { get; set; }

    // Size Limits (in inches)
    [Column("minimum_size_inches")]
    public decimal? MinimumSizeInches { get; set; }

    [Column("maximum_size_inches")]
    public decimal? MaximumSizeInches { get; set; }

    [Column("protected_slot_min_inches")]
    public decimal? ProtectedSlotMinInches { get; set; }

    [Column("protected_slot_max_inches")]
    public decimal? ProtectedSlotMaxInches { get; set; }

    // Special Regulations (stored as arrays in PostgreSQL)
    [Column("special_regulations")]
    public List<string> SpecialRegulations { get; set; } = new();

    [Column("required_stamps")]
    public List<string> RequiredStamps { get; set; } = new();

    // Additional notes
    [Column("notes")]
    public string? Notes { get; set; }

    // Metadata
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

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
            // If it's catch and release only, consider it always "open" for fishing
            if (!SeasonStartMonth.HasValue) return false;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var currentYear = today.Year;

            // Handle seasons that cross year boundaries
            var openDate = new DateOnly(currentYear, SeasonStartMonth.Value, SeasonStartDay ?? 1);
            var closeDate = SeasonEndMonth.HasValue 
                ? new DateOnly(currentYear, SeasonEndMonth.Value, SeasonEndDay ?? DateTime.DaysInMonth(currentYear, SeasonEndMonth.Value))
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
    public bool HasProtectedSlot => ProtectedSlotMinInches.HasValue && ProtectedSlotMaxInches.HasValue;

    [NotMapped]
    public string DisplaySeason
    {
        get
        {
            if (!SeasonStartMonth.HasValue) return "Season dates not specified";

            var openStr = new DateOnly(2000, SeasonStartMonth.Value, SeasonStartDay ?? 1).ToString("MMM d");
            var closeStr = SeasonEndMonth.HasValue 
                ? new DateOnly(2000, SeasonEndMonth.Value, SeasonEndDay ?? DateTime.DaysInMonth(2000, SeasonEndMonth.Value)).ToString("MMM d")
                : "Year End";
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
                parts.Add(slot);
            }

            return parts.Any() ? string.Join(", ", parts) : "No size limits";
        }
    }
}
