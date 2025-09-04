using FishingRegs.Services.Models;
using FishingRegs.Data.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for populating database tables with extracted fishing regulation data
/// </summary>
public interface IRegulationDatabasePopulationService
{
    /// <summary>
    /// Processes AI extracted lake regulations and populates the database
    /// </summary>
    /// <param name="extractionResult">Result from AI lake regulation extraction</param>
    /// <param name="sourceDocumentId">ID of the source document that contained the regulations</param>
    /// <param name="regulationYear">Year these regulations are effective for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing population statistics and any errors</returns>
    Task<RegulationPopulationResult> PopulateDatabaseAsync(
        AiLakeRegulationExtractionResult extractionResult,
        Guid sourceDocumentId,
        int regulationYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single lake regulation and creates/updates database records
    /// </summary>
    /// <param name="lakeRegulation">AI extracted lake regulation data</param>
    /// <param name="sourceDocumentId">ID of the source document</param>
    /// <param name="regulationYear">Year these regulations are effective for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing created/updated entities</returns>
    Task<SingleLakePopulationResult> PopulateSingleLakeAsync(
        AiLakeRegulation lakeRegulation,
        Guid sourceDocumentId,
        int regulationYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds or creates a water body based on extracted lake information
    /// </summary>
    /// <param name="lakeName">Name of the lake</param>
    /// <param name="county">County where the lake is located</param>
    /// <param name="stateId">State ID (defaults to Minnesota if not specified)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Water body entity (existing or newly created)</returns>
    Task<WaterBody> FindOrCreateWaterBodyAsync(
        string lakeName,
        string county,
        int stateId = 1, // Default to Minnesota
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds or creates fish species records based on extracted species names
    /// </summary>
    /// <param name="speciesNames">List of species names from AI extraction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping species names to fish species entities</returns>
    Task<Dictionary<string, FishSpecies>> FindOrCreateFishSpeciesAsync(
        IEnumerable<string> speciesNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and cleans up extracted regulation data before database insertion
    /// </summary>
    /// <param name="regulation">AI extracted regulation data</param>
    /// <returns>Validation result with cleaned data and any warnings</returns>
    RegulationValidationResult ValidateAndCleanRegulation(AiSpecialRegulation regulation);
}

/// <summary>
/// Result of populating the database with extracted regulations
/// </summary>
public class RegulationPopulationResult
{
    public bool IsSuccess { get; set; }
    public int TotalLakesProcessed { get; set; }
    public int WaterBodiesCreated { get; set; }
    public int WaterBodiesUpdated { get; set; }
    public int RegulationsCreated { get; set; }
    public int RegulationsUpdated { get; set; }
    public int FishSpeciesCreated { get; set; }
    public List<string> ProcessingWarnings { get; set; } = new();
    public List<string> ProcessingErrors { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// Result of populating a single lake's regulations
/// </summary>
public class SingleLakePopulationResult
{
    public bool IsSuccess { get; set; }
    public WaterBody? WaterBody { get; set; }
    public List<FishingRegulation> CreatedRegulations { get; set; } = new();
    public List<FishingRegulation> UpdatedRegulations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of validating and cleaning regulation data
/// </summary>
public class RegulationValidationResult
{
    public bool IsValid { get; set; }
    public AiSpecialRegulation CleanedRegulation { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
}
