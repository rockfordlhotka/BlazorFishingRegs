using FishingRegs.Services.Services;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for comprehensive database population using systematic AI extraction
/// Populates counties, water bodies, fish species, and regulations in the correct order
/// </summary>
public interface IComprehensiveDatabasePopulationService
{
    /// <summary>
    /// Performs comprehensive database population from the regulations text
    /// Extracts and populates: counties ? fish species ? water bodies ? regulations
    /// </summary>
    Task<ComprehensivePopulationResult> PopulateAllDataAsync(
        string regulationsText, 
        Guid sourceDocumentId, 
        int regulationYear);
}