namespace FishingRegs.Services.Models;

/// <summary>
/// Extracted fishing regulation data from text processing
/// </summary>
public class FishingRegulationData
{
    public string DocumentName { get; set; } = string.Empty;
    public List<LakeRegulation> LakeRegulations { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public double OverallConfidence { get; set; }
    public int TotalLakesProcessed { get; set; }
    public int TotalRegulationsExtracted { get; set; }
}

/// <summary>
/// Fishing regulation for a specific lake
/// </summary>
public class LakeRegulation
{
    public string LakeName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public List<SpeciesRegulation> Species { get; set; } = new();
    public List<string> SpecialRegulations { get; set; } = new();
    public double Confidence { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Fishing regulation for a specific species
/// </summary>
public class SpeciesRegulation
{
    public string SpeciesName { get; set; } = string.Empty;
    public int? DailyLimit { get; set; }
    public int? PossessionLimit { get; set; }
    public decimal? MinimumSizeInches { get; set; }
    public decimal? MaximumSizeInches { get; set; }
    public string? SeasonInfo { get; set; }
    public string? SizeRestrictions { get; set; }
    public List<string> BaitRestrictions { get; set; } = new();
    public List<string> GearRestrictions { get; set; } = new();
    public bool IsCatchAndRelease { get; set; }
    public double Confidence { get; set; }
    public string? Notes { get; set; }
}
