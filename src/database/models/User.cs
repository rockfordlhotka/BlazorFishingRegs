using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingRegs.Data.Models;

/// <summary>
/// Represents a user of the application
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(255)]
    [Column("azure_ad_object_id")]
    public string? AzureAdObjectId { get; set; }

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("first_name")]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    [Column("last_name")]
    public string? LastName { get; set; }

    [MaxLength(200)]
    [Column("display_name")]
    public string? DisplayName { get; set; }

    [MaxLength(20)]
    [Phone]
    [Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Column("preferred_state_id")]
    public int? PreferredStateId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("user_role")]
    public string UserRole { get; set; } = "angler";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_login_at")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("PreferredStateId")]
    public virtual State? PreferredState { get; set; }

    public virtual ICollection<UserFavorite> Favorites { get; set; } = new List<UserFavorite>();
    public virtual ICollection<SearchHistory> SearchHistory { get; set; } = new List<SearchHistory>();
    public virtual ICollection<RegulationAuditLog> AuditLogEntries { get; set; } = new List<RegulationAuditLog>();

    // Computed properties
    [NotMapped]
    public string FullName => string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName) 
        ? DisplayName ?? Email 
        : $"{FirstName} {LastName}";

    [NotMapped]
    public bool IsAdmin => UserRole == "admin";

    [NotMapped]
    public bool IsModerator => UserRole == "moderator" || IsAdmin;

    [NotMapped]
    public bool CanReviewRegulations => UserRole == "moderator" || UserRole == "admin";

    [NotMapped]
    public bool CanUploadDocuments => UserRole == "contributor" || UserRole == "moderator" || UserRole == "admin";
}

/// <summary>
/// Represents a user's favorite water body
/// </summary>
[Table("user_favorites")]
public class UserFavorite
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("water_body_id")]
    public int WaterBodyId { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("WaterBodyId")]
    public virtual WaterBody WaterBody { get; set; } = null!;
}
