using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents a water body (lake, river, stream, etc.)
/// </summary>
[Table("water_bodies")]
public class WaterBody
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("state_id")]
    public int StateId { get; set; }

    [Column("county_id")]
    public int? CountyId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string WaterType { get; set; } = "lake";

    [MaxLength(50)]
    [Column("dnr_water_id")]
    public string? DnrId { get; set; }

    [Column("latitude")]
    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Column("surface_area_acres")]
    public decimal? SurfaceAreaAcres { get; set; }

    [Column("max_depth_feet")]
    public decimal? MaxDepthFeet { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("StateId")]
    public virtual State State { get; set; } = null!;

    [ForeignKey("CountyId")]
    public virtual County? County { get; set; }

    public virtual ICollection<WaterBodySpecies> WaterBodySpecies { get; set; } = new List<WaterBodySpecies>();
    public virtual ICollection<FishingRegulation> FishingRegulations { get; set; } = new List<FishingRegulation>();
    public virtual ICollection<UserFavorite> UserFavorites { get; set; } = new List<UserFavorite>();
}

/// <summary>
/// Junction table for water bodies and fish species
/// </summary>
[Table("water_body_species")]
public class WaterBodySpecies
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("water_body_id")]
    public int WaterBodyId { get; set; }

    [Required]
    [Column("species_id")]
    public int SpeciesId { get; set; }

    [Column("is_stocked")]
    public bool IsStocked { get; set; } = false;

    [MaxLength(20)]
    [Column("stocking_frequency")]
    public string? StockingFrequency { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("WaterBodyId")]
    public virtual WaterBody WaterBody { get; set; } = null!;

    [ForeignKey("SpeciesId")]
    public virtual FishSpecies Species { get; set; } = null!;
}
