using System.Text.Json.Serialization;

namespace FishingRegs.Services.Models;

/// <summary>
/// Represents a complete lake regulation set extracted from the fishing regulations document
/// </summary>
public class AiLakeRegulation
{
    [JsonPropertyName("lakeId")]
    public int LakeId { get; set; }

    [JsonPropertyName("lakeName")]
    public string LakeName { get; set; } = string.Empty;

    [JsonPropertyName("county")]
    public string County { get; set; } = string.Empty;

    [JsonPropertyName("regulations")]
    public AiRegulationDetails Regulations { get; set; } = new();
}

/// <summary>
/// Contains the detailed regulations for a specific lake
/// </summary>
public class AiRegulationDetails
{
    [JsonPropertyName("specialRegulations")]
    public List<AiSpecialRegulation> SpecialRegulations { get; set; } = new();

    [JsonPropertyName("generalNotes")]
    public string GeneralNotes { get; set; } = string.Empty;

    [JsonPropertyName("isExperimental")]
    public bool IsExperimental { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a specific regulation rule for a fish species
/// </summary>
public class AiSpecialRegulation
{
    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("regulationType")]
    public AiRegulationType RegulationType { get; set; }

    [JsonPropertyName("dailyLimit")]
    public int? DailyLimit { get; set; }

    [JsonPropertyName("possessionLimit")]
    public int? PossessionLimit { get; set; }

    [JsonPropertyName("minimumSize")]
    public string? MinimumSize { get; set; }

    [JsonPropertyName("maximumSize")]
    public string? MaximumSize { get; set; }

    [JsonPropertyName("protectedSlot")]
    public string? ProtectedSlot { get; set; }

    [JsonPropertyName("seasonInfo")]
    public string? SeasonInfo { get; set; }

    [JsonPropertyName("catchAndRelease")]
    public bool CatchAndRelease { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Types of regulations that can be applied
/// </summary>
public enum AiRegulationType
{
    DailyLimit,
    PossessionLimit,
    SizeLimit,
    ProtectedSlot,
    CatchAndRelease,
    Seasonal,
    Combined
}

/// <summary>
/// Result of the AI-based lake regulation extraction process
/// </summary>
public class AiLakeRegulationExtractionResult
{
    public bool IsSuccess { get; set; }
    public List<AiLakeRegulation> ExtractedRegulations { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public int TotalLakesProcessed { get; set; }
    public int TotalRegulationsExtracted { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public List<string> ProcessingWarnings { get; set; } = new();
}
