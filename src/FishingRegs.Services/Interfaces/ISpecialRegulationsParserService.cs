using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for parsing special regulations text files without AI
/// </summary>
public interface ISpecialRegulationsParserService
{
    /// <summary>
    /// Parses the special regulations text file and extracts lake regulation data
    /// </summary>
    /// <param name="regulationsText">The full text content of the special regulations file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing parsed lake regulations</returns>
    Task<SpecialRegulationsParseResult> ParseSpecialRegulationsAsync(
        string regulationsText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all previous regulation data for the specified year
    /// </summary>
    /// <param name="regulationYear">The year to clear regulations for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing count of deleted records</returns>
    Task<RegulationClearResult> ClearPreviousRegulationsAsync(
        int regulationYear,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing special regulations
/// </summary>
public class SpecialRegulationsParseResult
{
    public bool IsSuccess { get; set; }
    public int TotalLakesParsed { get; set; }
    public int TotalSpeciesRegulationsParsed { get; set; }
    public List<ParsedLakeEntry> ParsedLakes { get; set; } = new();
    public List<string> ParseWarnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// Represents a parsed lake entry from special regulations
/// </summary>
public class ParsedLakeEntry
{
    public string LakeName { get; set; } = "";
    public string County { get; set; } = "";
    public string RegulationText { get; set; } = "";
    public bool HasCrossReference { get; set; }
    public bool IsCompoundEntry { get; set; }
    public List<ParsedSpeciesRegulation> SpeciesRegulations { get; set; } = new();
}

/// <summary>
/// Represents a species-specific regulation parsed from text
/// </summary>
public class ParsedSpeciesRegulation
{
    public string Species { get; set; } = "";
    public string RegulationType { get; set; } = "";
    public string RegulationDetails { get; set; } = "";
    public int? DailyLimit { get; set; }
    public int? PossessionLimit { get; set; }
    public string? MinimumSize { get; set; }
    public string? MaximumSize { get; set; }
    public string? ProtectedSlot { get; set; }
    public bool IsCatchAndRelease { get; set; }
}

/// <summary>
/// Result of clearing previous regulations
/// </summary>
public class RegulationClearResult
{
    public bool IsSuccess { get; set; }
    public int RegulationsDeleted { get; set; }
    public int WaterBodiesAffected { get; set; }
    public string? ErrorMessage { get; set; }
}
