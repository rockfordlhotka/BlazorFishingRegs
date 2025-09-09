using FishingRegs.Services.Services;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for comprehensive data extraction from fishing regulations documents
/// Extracts all counties, water bodies, fish species, and regulations systematically
/// </summary>
public interface IComprehensiveDataExtractionService
{
    /// <summary>
    /// Extracts all counties mentioned in the fishing regulations document
    /// </summary>
    Task<ComprehensiveExtractionResult<List<CountyData>>> ExtractAllCountiesAsync(string regulationsText);

    /// <summary>
    /// Extracts all water bodies (lakes, rivers, streams) with their counties
    /// </summary>
    Task<ComprehensiveExtractionResult<List<WaterBodyData>>> ExtractAllWaterBodiesAsync(string regulationsText);

    /// <summary>
    /// Extracts all fish species mentioned in the regulations
    /// </summary>
    Task<ComprehensiveExtractionResult<List<FishSpeciesData>>> ExtractAllFishSpeciesAsync(string regulationsText);

    /// <summary>
    /// Extracts all regulations per water body - the comprehensive approach
    /// </summary>
    Task<ComprehensiveExtractionResult<List<WaterBodyRegulationData>>> ExtractAllRegulationsAsync(string regulationsText);
}