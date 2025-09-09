using FishingRegs.Data;
using FishingRegs.Data.Models;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Services;
using Microsoft.Extensions.Logging;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for comprehensive database population using systematic AI extraction
/// Populates counties, water bodies, fish species, and regulations in the correct order
/// </summary>
public class ComprehensiveDatabasePopulationService : IComprehensiveDatabasePopulationService
{
    private readonly ILogger<ComprehensiveDatabasePopulationService> _logger;
    private readonly IComprehensiveDataExtractionService _extractionService;
    private readonly IUnitOfWork _unitOfWork;

    public ComprehensiveDatabasePopulationService(
        ILogger<ComprehensiveDatabasePopulationService> logger,
        IComprehensiveDataExtractionService extractionService,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _extractionService = extractionService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Performs comprehensive database population from the regulations text
    /// Extracts and populates: counties ? fish species ? water bodies ? regulations
    /// </summary>
    public async Task<ComprehensivePopulationResult> PopulateAllDataAsync(
        string regulationsText, 
        Guid sourceDocumentId, 
        int regulationYear)
    {
        var result = new ComprehensivePopulationResult();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting comprehensive database population");

            // Step 1: Extract and populate counties
            _logger.LogInformation("Step 1: Extracting counties...");
            var countiesResult = await _extractionService.ExtractAllCountiesAsync(regulationsText);
            if (!countiesResult.IsSuccess)
            {
                result.ErrorMessage = $"County extraction failed: {countiesResult.ErrorMessage}";
                return result;
            }

            result.CountiesProcessed = await PopulateCountiesAsync(countiesResult.Data ?? new List<CountyData>());
            _logger.LogInformation("Counties populated: {Count}", result.CountiesProcessed);

            // Step 2: Extract and populate fish species
            _logger.LogInformation("Step 2: Extracting fish species...");
            var speciesResult = await _extractionService.ExtractAllFishSpeciesAsync(regulationsText);
            if (!speciesResult.IsSuccess)
            {
                result.ErrorMessage = $"Fish species extraction failed: {speciesResult.ErrorMessage}";
                return result;
            }

            result.FishSpeciesProcessed = await PopulateFishSpeciesAsync(speciesResult.Data ?? new List<FishSpeciesData>());
            _logger.LogInformation("Fish species populated: {Count}", result.FishSpeciesProcessed);

            // Step 3: Extract and populate water bodies
            _logger.LogInformation("Step 3: Extracting water bodies...");
            var waterBodiesResult = await _extractionService.ExtractAllWaterBodiesAsync(regulationsText);
            if (!waterBodiesResult.IsSuccess)
            {
                result.ErrorMessage = $"Water bodies extraction failed: {waterBodiesResult.ErrorMessage}";
                return result;
            }

            var (waterBodiesProcessed, createdCounties) = await PopulateWaterBodiesAsync(waterBodiesResult.Data ?? new List<WaterBodyData>());
            result.WaterBodiesProcessed = waterBodiesProcessed;
            result.CountiesCreatedDuringProcessing = createdCounties.Count;
            result.CountiesCreatedOnDemand = createdCounties;
            _logger.LogInformation("Water bodies populated: {Count}, Counties created on-demand: {CountiesCount}", 
                result.WaterBodiesProcessed, result.CountiesCreatedDuringProcessing);

            // Step 4: Extract and populate regulations
            _logger.LogInformation("Step 4: Extracting regulations...");
            var regulationsResult = await _extractionService.ExtractAllRegulationsAsync(regulationsText);
            if (!regulationsResult.IsSuccess)
            {
                result.ErrorMessage = $"Regulations extraction failed: {regulationsResult.ErrorMessage}";
                return result;
            }

            result.RegulationsProcessed = await PopulateRegulationsAsync(
                regulationsResult.Data ?? new List<WaterBodyRegulationData>(), 
                sourceDocumentId, 
                regulationYear);
            _logger.LogInformation("Regulations populated: {Count}", result.RegulationsProcessed);

            // Save all changes
            await _unitOfWork.SaveChangesAsync();

            result.IsSuccess = true;
            result.ProcessingTime = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("Comprehensive database population completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during comprehensive database population");
            result.ErrorMessage = ex.Message;
            result.ProcessingTime = DateTime.UtcNow - startTime;
        }

        return result;
    }

    /// <summary>
    /// Populates counties in the database
    /// </summary>
    private async Task<int> PopulateCountiesAsync(List<CountyData> counties)
    {
        var processed = 0;
        var minnesotaStateId = 1; // Assuming Minnesota state ID is 1

        foreach (var countyData in counties)
        {
            try
            {
                // Check if county already exists
                var existingCounty = await _unitOfWork.Counties
                    .FirstOrDefaultAsync(c => c.Name == countyData.Name && c.StateId == minnesotaStateId);

                if (existingCounty == null)
                {
                    var newCounty = new County
                    {
                        Name = countyData.Name,
                        StateId = minnesotaStateId,
                        FipsCode = countyData.FipsCode,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.Counties.AddAsync(newCounty);
                    processed++;
                    _logger.LogDebug("Added new county: {CountyName}", countyData.Name);
                }
                else
                {
                    _logger.LogDebug("County already exists: {CountyName}", countyData.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing county: {CountyName}", countyData.Name);
            }
        }

        return processed;
    }

    /// <summary>
    /// Populates fish species in the database
    /// </summary>
    private async Task<int> PopulateFishSpeciesAsync(List<FishSpeciesData> fishSpecies)
    {
        var processed = 0;

        foreach (var speciesData in fishSpecies)
        {
            try
            {
                // Check if species already exists
                var existingSpecies = await _unitOfWork.FishSpecies
                    .FirstOrDefaultAsync(fs => fs.CommonName == speciesData.CommonName);

                if (existingSpecies == null)
                {
                    var newSpecies = new FishSpecies
                    {
                        CommonName = speciesData.CommonName,
                        ScientificName = speciesData.ScientificName,
                        SpeciesCode = speciesData.SpeciesCode,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.FishSpecies.AddAsync(newSpecies);
                    processed++;
                    _logger.LogDebug("Added new fish species: {SpeciesName}", speciesData.CommonName);
                }
                else
                {
                    // Update existing species if needed
                    if (string.IsNullOrEmpty(existingSpecies.ScientificName) && !string.IsNullOrEmpty(speciesData.ScientificName))
                    {
                        existingSpecies.ScientificName = speciesData.ScientificName;
                        existingSpecies.UpdatedAt = DateTimeOffset.UtcNow;
                        _logger.LogDebug("Updated scientific name for species: {SpeciesName}", speciesData.CommonName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing fish species: {SpeciesName}", speciesData.CommonName);
            }
        }

        return processed;
    }

    /// <summary>
    /// Populates water bodies in the database
    /// </summary>
    private async Task<(int processed, List<string> createdCounties)> PopulateWaterBodiesAsync(List<WaterBodyData> waterBodies)
    {
        var processed = 0;
        var minnesotaStateId = 1; // Assuming Minnesota state ID is 1
        var createdCounties = new List<string>(); // Track counties we've created in this session

        foreach (var waterBodyData in waterBodies)
        {
            try
            {
                // Find county ID - create county if it doesn't exist
                var county = await _unitOfWork.Counties
                    .FirstOrDefaultAsync(c => c.Name == waterBodyData.County && c.StateId == minnesotaStateId);

                if (county == null)
                {
                    // County doesn't exist - create it
                    _logger.LogInformation("County not found, creating new county: {CountyName}", waterBodyData.County);
                    
                    var newCounty = new County
                    {
                        Name = waterBodyData.County,
                        StateId = minnesotaStateId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.Counties.AddAsync(newCounty);
                    await _unitOfWork.SaveChangesAsync(); // Save immediately to get the ID
                    
                    county = newCounty;
                    createdCounties.Add(waterBodyData.County);
                    
                    _logger.LogInformation("Created new county: {CountyName} with ID: {CountyId}", 
                        waterBodyData.County, county.Id);
                }

                // Check if water body already exists
                var existingWaterBody = await _unitOfWork.WaterBodies
                    .FirstOrDefaultAsync(wb => wb.Name == waterBodyData.Name && wb.CountyId == county.Id);

                if (existingWaterBody == null)
                {
                    var newWaterBody = new WaterBody
                    {
                        Name = waterBodyData.Name,
                        StateId = minnesotaStateId,
                        CountyId = county.Id,
                        WaterType = waterBodyData.WaterType,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.WaterBodies.AddAsync(newWaterBody);
                    processed++;
                    _logger.LogDebug("Added new water body: {WaterBodyName} ({County})", waterBodyData.Name, waterBodyData.County);
                }
                else
                {
                    _logger.LogDebug("Water body already exists: {WaterBodyName} ({County})", waterBodyData.Name, waterBodyData.County);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing water body: {WaterBodyName}", waterBodyData.Name);
            }
        }

        // Log summary of created counties
        if (createdCounties.Any())
        {
            _logger.LogInformation("Created {Count} new counties during water body processing: {Counties}", 
                createdCounties.Count, string.Join(", ", createdCounties));
        }

        return (processed, createdCounties);
    }

    /// <summary>
    /// Populates regulations in the database
    /// </summary>
    private async Task<int> PopulateRegulationsAsync(
        List<WaterBodyRegulationData> waterBodyRegulations, 
        Guid sourceDocumentId, 
        int regulationYear)
    {
        var processed = 0;
        var minnesotaStateId = 1;

        foreach (var waterBodyRegData in waterBodyRegulations)
        {
            try
            {
                // Find the county - create it if it doesn't exist
                var county = await _unitOfWork.Counties
                    .FirstOrDefaultAsync(c => c.Name == waterBodyRegData.County && c.StateId == minnesotaStateId);

                if (county == null)
                {
                    // County doesn't exist - create it
                    _logger.LogInformation("County not found during regulation processing, creating: {CountyName}", waterBodyRegData.County);
                    
                    var newCounty = new County
                    {
                        Name = waterBodyRegData.County,
                        StateId = minnesotaStateId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.Counties.AddAsync(newCounty);
                    await _unitOfWork.SaveChangesAsync(); // Save immediately to get the ID
                    
                    county = newCounty;
                    _logger.LogInformation("Created new county during regulation processing: {CountyName} with ID: {CountyId}", 
                        waterBodyRegData.County, county.Id);
                }

                var waterBody = await _unitOfWork.WaterBodies
                    .FirstOrDefaultAsync(wb => wb.Name == waterBodyRegData.WaterBodyName && wb.CountyId == county.Id);

                if (waterBody == null)
                {
                    // Water body doesn't exist - create it
                    _logger.LogInformation("Water body not found during regulation processing, creating: {WaterBodyName} ({County})", 
                        waterBodyRegData.WaterBodyName, waterBodyRegData.County);
                    
                    var newWaterBody = new WaterBody
                    {
                        Name = waterBodyRegData.WaterBodyName,
                        StateId = minnesotaStateId,
                        CountyId = county.Id,
                        WaterType = waterBodyRegData.WaterType,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.WaterBodies.AddAsync(newWaterBody);
                    await _unitOfWork.SaveChangesAsync(); // Save immediately to get the ID
                    
                    waterBody = newWaterBody;
                    _logger.LogInformation("Created new water body during regulation processing: {WaterBodyName} with ID: {WaterBodyId}", 
                        waterBodyRegData.WaterBodyName, waterBody.Id);
                }

                // Process each regulation
                foreach (var regData in waterBodyRegData.Regulations)
                {
                    try
                    {
                        // Find the fish species - create it if it doesn't exist
                        var fishSpecies = await _unitOfWork.FishSpecies
                            .FirstOrDefaultAsync(fs => fs.CommonName == regData.Species);

                        if (fishSpecies == null)
                        {
                            // Fish species doesn't exist - create it
                            _logger.LogInformation("Fish species not found during regulation processing, creating: {SpeciesName}", regData.Species);
                            
                            var newFishSpecies = new FishSpecies
                            {
                                CommonName = regData.Species,
                                IsActive = true,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                            };

                            await _unitOfWork.FishSpecies.AddAsync(newFishSpecies);
                            await _unitOfWork.SaveChangesAsync(); // Save immediately to get the ID
                            
                            fishSpecies = newFishSpecies;
                            _logger.LogInformation("Created new fish species during regulation processing: {SpeciesName} with ID: {SpeciesId}", 
                                regData.Species, fishSpecies.Id);
                        }

                        // Check if regulation already exists for this year
                        var existingRegulation = await _unitOfWork.FishingRegulations
                            .FirstOrDefaultAsync(fr => fr.WaterBodyId == waterBody.Id && 
                                                      fr.SpeciesId == fishSpecies.Id && 
                                                      fr.RegulationYear == regulationYear);

                        if (existingRegulation == null)
                        {
                            var newRegulation = new FishingRegulation
                            {
                                WaterBodyId = waterBody.Id,
                                SpeciesId = fishSpecies.Id,
                                RegulationYear = regulationYear,
                                SourceDocumentId = sourceDocumentId,
                                RegulationType = regData.RegulationType.ToString(),
                                EffectiveDate = new DateOnly(regulationYear, 1, 1),
                                ExpirationDate = new DateOnly(regulationYear, 12, 31),
                                SeasonStartMonth = regData.SeasonStartMonth,
                                SeasonStartDay = regData.SeasonStartDay,
                                SeasonEndMonth = regData.SeasonEndMonth,
                                SeasonEndDay = regData.SeasonEndDay,
                                IsCatchAndRelease = regData.IsCatchAndRelease,
                                DailyLimit = regData.DailyLimit,
                                PossessionLimit = regData.PossessionLimit,
                                MinimumSizeInches = regData.MinimumSizeInches,
                                MaximumSizeInches = regData.MaximumSizeInches,
                                ProtectedSlotMinInches = regData.ProtectedSlotMinInches,
                                ProtectedSlotMaxInches = regData.ProtectedSlotMaxInches,
                                SpecialRegulations = regData.SpecialRegulations,
                                Notes = regData.Notes,
                                IsActive = true,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                            };

                            await _unitOfWork.FishingRegulations.AddAsync(newRegulation);
                            processed++;
                            _logger.LogDebug("Added regulation: {WaterBody} - {Species} - {RegType}", 
                                waterBody.Name, fishSpecies.CommonName, regData.RegulationType);
                        }
                        else
                        {
                            _logger.LogDebug("Regulation already exists: {WaterBody} - {Species}", 
                                waterBody.Name, fishSpecies.CommonName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing regulation for {WaterBody} - {Species}", 
                            waterBodyRegData.WaterBodyName, regData.Species);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing regulations for water body: {WaterBodyName}", waterBodyRegData.WaterBodyName);
            }
        }

        return processed;
    }
}

/// <summary>
/// Result of comprehensive database population
/// </summary>
public class ComprehensivePopulationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int CountiesProcessed { get; set; }
    public int CountiesCreatedDuringProcessing { get; set; } // NEW: Counties created on-demand
    public int FishSpeciesProcessed { get; set; }
    public int WaterBodiesProcessed { get; set; }
    public int RegulationsProcessed { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public List<string> ProcessingWarnings { get; set; } = new();
    public List<string> CountiesCreatedOnDemand { get; set; } = new(); // NEW: List of counties created

    public int TotalItemsProcessed => CountiesProcessed + CountiesCreatedDuringProcessing + FishSpeciesProcessed + WaterBodiesProcessed + RegulationsProcessed;
}