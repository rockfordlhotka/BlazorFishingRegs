using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents a state or province
/// </summary>
[Table("states")]
public class State
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(2)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    [Column("country")]
    public string Country { get; set; } = "US";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual ICollection<County> Counties { get; set; } = new List<County>();
    public virtual ICollection<WaterBody> WaterBodies { get; set; } = new List<WaterBody>();
    public virtual ICollection<RegulationDocument> RegulationDocuments { get; set; } = new List<RegulationDocument>();
    public virtual ICollection<User> PreferredByUsers { get; set; } = new List<User>();
}

/// <summary>
/// Represents a county within a state
/// </summary>
[Table("counties")]
public class County
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("state_id")]
    public int StateId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(5)]
    [Column("fips_code")]
    public string? FipsCode { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("StateId")]
    public virtual State State { get; set; } = null!;
    public virtual ICollection<WaterBody> WaterBodies { get; set; } = new List<WaterBody>();
}

/// <summary>
/// Represents a fish species
/// </summary>
[Table("fish_species")]
public class FishSpecies
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("common_name")]
    public string CommonName { get; set; } = string.Empty;

    [MaxLength(150)]
    [Column("scientific_name")]
    public string? ScientificName { get; set; }

    [MaxLength(10)]
    [Column("species_code")]
    public string? SpeciesCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual ICollection<WaterBodySpecies> WaterBodySpecies { get; set; } = new List<WaterBodySpecies>();
    public virtual ICollection<FishingRegulation> FishingRegulations { get; set; } = new List<FishingRegulation>();
}
