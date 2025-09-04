using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents a user's search query for analytics
/// </summary>
[Table("search_history")]
public class SearchHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Required]
    [Column("search_query")]
    public string SearchQuery { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("search_type")]
    public string SearchType { get; set; } = string.Empty;

    [Column("results_count")]
    public int ResultsCount { get; set; } = 0;

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [MaxLength(255)]
    [Column("session_id")]
    public string? SessionId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}

/// <summary>
/// Audit log for tracking changes to fishing regulations
/// </summary>
[Table("regulation_audit_log")]
public class RegulationAuditLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("regulation_id")]
    public Guid RegulationId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("changed_fields")]
    public List<string> ChangedFields { get; set; } = new();

    [Column("old_values")]
    public string? OldValuesJson { get; set; } // JSONB stored as string

    [Column("new_values")]
    public string? NewValuesJson { get; set; } // JSONB stored as string

    [Column("changed_by")]
    public Guid? ChangedBy { get; set; }

    [Column("change_reason")]
    public string? ChangeReason { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("RegulationId")]
    public virtual FishingRegulation Regulation { get; set; } = null!;

    [ForeignKey("ChangedBy")]
    public virtual User? ChangedByUser { get; set; }
}

/// <summary>
/// View model for lake summary information
/// </summary>
[Table("lake_summary")]
public class LakeSummaryView
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("water_type")]
    public string WaterType { get; set; } = string.Empty;

    [Column("state_name")]
    public string StateName { get; set; } = string.Empty;

    [Column("state_code")]
    public string StateCode { get; set; } = string.Empty;

    [Column("county_name")]
    public string? CountyName { get; set; }

    [Column("latitude")]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    public decimal? Longitude { get; set; }

    [Column("surface_area_acres")]
    public decimal? SurfaceAreaAcres { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("regulation_count")]
    public int RegulationCount { get; set; }

    [Column("species_count")]
    public int SpeciesCount { get; set; }

    [Column("species_list")]
    public string? SpeciesList { get; set; }
}

/// <summary>
/// View model for current fishing regulations with all details
/// </summary>
[Table("current_fishing_regulations")]
public class CurrentFishingRegulationsView
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("water_body_name")]
    public string WaterBodyName { get; set; } = string.Empty;

    [Column("species_name")]
    public string SpeciesName { get; set; } = string.Empty;

    [Column("state_name")]
    public string StateName { get; set; } = string.Empty;

    [Column("regulation_year")]
    public int RegulationYear { get; set; }

    [Column("effective_date")]
    public DateOnly EffectiveDate { get; set; }

    [Column("expiration_date")]
    public DateOnly? ExpirationDate { get; set; }

    [Column("season_open_date")]
    public DateOnly? SeasonOpenDate { get; set; }

    [Column("season_close_date")]
    public DateOnly? SeasonCloseDate { get; set; }

    [Column("is_year_round")]
    public bool IsYearRound { get; set; }

    [Column("daily_limit")]
    public int? DailyLimit { get; set; }

    [Column("possession_limit")]
    public int? PossessionLimit { get; set; }

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

    [Column("special_regulations")]
    public List<string> SpecialRegulations { get; set; } = new();

    [Column("bait_restrictions")]
    public string? BaitRestrictions { get; set; }

    [Column("gear_restrictions")]
    public string? GearRestrictions { get; set; }

    [Column("requires_special_stamp")]
    public bool RequiresSpecialStamp { get; set; }

    [Column("required_stamps")]
    public List<string> RequiredStamps { get; set; } = new();

    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("review_status")]
    public string ReviewStatus { get; set; } = string.Empty;
}
