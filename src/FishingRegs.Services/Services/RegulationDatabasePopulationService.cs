using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using FishingRegs.Data.Models;
using FishingRegs.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for populating database tables with extracted fishing regulation data
/// </summary>
public class RegulationDatabasePopulationService : IRegulationDatabasePopulationService
{
    private readonly ILogger<RegulationDatabasePopulationService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    // Common fish species name mappings for standardization
    private static readonly Dictionary<string, string> SpeciesNameMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "lake trout", "Lake Trout" },
        { "laketrout", "Lake Trout" },
        { "salmon", "Salmon" },
        { "coho salmon", "Coho Salmon" },
        { "chinook salmon", "Chinook Salmon" },
        { "northern pike", "Northern Pike" },
        { "pike", "Northern Pike" },
        { "walleye", "Walleye" },
        { "bass", "Largemouth Bass" },
        { "largemouth bass", "Largemouth Bass" },
        { "smallmouth bass", "Smallmouth Bass" },
        { "muskie", "Muskellunge" },
        { "muskellunge", "Muskellunge" },
        { "brook trout", "Brook Trout" },
        { "brown trout", "Brown Trout" },
        { "rainbow trout", "Rainbow Trout" },
        { "steelhead", "Steelhead" },
        { "perch", "Yellow Perch" },
        { "yellow perch", "Yellow Perch" },
        { "bluegill", "Bluegill" },
        { "sunfish", "Bluegill" },
        { "crappie", "Crappie" },
        { "black crappie", "Black Crappie" },
        { "white crappie", "White Crappie" }
    };

    public RegulationDatabasePopulationService(
        ILogger<RegulationDatabasePopulationService> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegulationPopulationResult> PopulateDatabaseAsync(
        AiLakeRegulationExtractionResult extractionResult,
        Guid sourceDocumentId,
        int regulationYear,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RegulationPopulationResult();

        try
        {
            if (!extractionResult.IsSuccess)
            {
                result.ErrorMessage = $"Cannot process failed extraction: {extractionResult.ErrorMessage}";
                return result;
            }

            _logger.LogInformation($"Starting database population for {extractionResult.ExtractedRegulations.Count} lakes");

            // Process each lake regulation
            foreach (var lakeRegulation in extractionResult.ExtractedRegulations)
            {
                try
                {
                    var lakeResult = await PopulateSingleLakeAsync(lakeRegulation, sourceDocumentId, regulationYear, cancellationToken);
                    
                    if (lakeResult.IsSuccess)
                    {
                        if (lakeResult.WaterBody != null)
                        {
                            if (lakeResult.WaterBody.Id == 0) // New water body
                                result.WaterBodiesCreated++;
                            else
                                result.WaterBodiesUpdated++;
                        }

                        result.RegulationsCreated += lakeResult.CreatedRegulations.Count;
                        result.RegulationsUpdated += lakeResult.UpdatedRegulations.Count;
                    }

                    result.ProcessingWarnings.AddRange(lakeResult.Warnings);
                    if (!string.IsNullOrEmpty(lakeResult.ErrorMessage))
                    {
                        result.ProcessingErrors.Add($"Lake {lakeRegulation.LakeName}: {lakeResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to process lake: {lakeRegulation.LakeName}");
                    result.ProcessingErrors.Add($"Lake {lakeRegulation.LakeName}: {ex.Message}");
                }

                result.TotalLakesProcessed++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            result.IsSuccess = result.ProcessingErrors.Count == 0;

            _logger.LogInformation($"Database population completed. Processed {result.TotalLakesProcessed} lakes, " +
                                 $"created {result.WaterBodiesCreated} water bodies, {result.RegulationsCreated} regulations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database population");
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<SingleLakePopulationResult> PopulateSingleLakeAsync(
        AiLakeRegulation lakeRegulation,
        Guid sourceDocumentId,
        int regulationYear,
        CancellationToken cancellationToken = default)
    {
        var result = new SingleLakePopulationResult();

        try
        {
            // Find or create the water body
            result.WaterBody = await FindOrCreateWaterBodyAsync(
                lakeRegulation.LakeName, 
                lakeRegulation.County, 
                1, // Minnesota
                cancellationToken);

            if (result.WaterBody == null)
            {
                result.ErrorMessage = $"Could not create or find water body for {lakeRegulation.LakeName}";
                return result;
            }

            // Get all unique species from regulations
            var speciesNames = lakeRegulation.Regulations.SpecialRegulations
                .Select(sr => sr.Species)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            if (!speciesNames.Any())
            {
                result.Warnings.Add($"No species found in regulations for {lakeRegulation.LakeName}");
                result.IsSuccess = true;
                return result;
            }

            // Find or create fish species
            var fishSpeciesMap = await FindOrCreateFishSpeciesAsync(speciesNames, cancellationToken);

            // Process each special regulation
            foreach (var specialRegulation in lakeRegulation.Regulations.SpecialRegulations)
            {
                var validationResult = ValidateAndCleanRegulation(specialRegulation);
                
                if (!validationResult.IsValid)
                {
                    result.Warnings.AddRange(validationResult.ValidationErrors);
                    continue;
                }

                result.Warnings.AddRange(validationResult.ValidationWarnings);

                if (!fishSpeciesMap.TryGetValue(specialRegulation.Species, out var fishSpecies))
                {
                    result.Warnings.Add($"Could not find fish species for: {specialRegulation.Species}");
                    continue;
                }

                // Create fishing regulation record
                var fishingRegulation = CreateFishingRegulationFromAi(
                    validationResult.CleanedRegulation,
                    result.WaterBody.Id,
                    fishSpecies.Id,
                    sourceDocumentId,
                    regulationYear);

                // Check if a similar regulation already exists
                var existingRegulations = await _unitOfWork.FishingRegulations
                    .GetByWaterBodyAndSpeciesAsync(result.WaterBody.Id, fishSpecies.Id, cancellationToken);

                var existingRegulation = existingRegulations
                    .FirstOrDefault(fr => fr.RegulationYear == regulationYear && fr.IsActive);

                if (existingRegulation != null)
                {
                    // Update existing regulation
                    UpdateFishingRegulationFromAi(existingRegulation, validationResult.CleanedRegulation);
                    existingRegulation.UpdatedAt = DateTimeOffset.UtcNow;
                    result.UpdatedRegulations.Add(existingRegulation);
                }
                else
                {
                    // Add new regulation
                    await _unitOfWork.FishingRegulations.AddAsync(fishingRegulation, cancellationToken);
                    result.CreatedRegulations.Add(fishingRegulation);
                }
            }

            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing lake: {lakeRegulation.LakeName}");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<WaterBody> FindOrCreateWaterBodyAsync(
        string lakeName,
        string county,
        int stateId = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lakeName))
            throw new ArgumentException("Lake name cannot be empty", nameof(lakeName));

        // First, try to find existing water body
        var existingWaterBodies = await _unitOfWork.WaterBodies.SearchByNameAsync(lakeName, cancellationToken);
        var existingWaterBody = existingWaterBodies.FirstOrDefault(wb => 
            wb.StateId == stateId && 
            string.Equals(wb.Name, lakeName, StringComparison.OrdinalIgnoreCase));

        if (existingWaterBody != null)
        {
            _logger.LogDebug($"Found existing water body: {lakeName}");
            return existingWaterBody;
        }

        // Find county if provided
        int? countyId = null;
        if (!string.IsNullOrWhiteSpace(county))
        {
            var counties = await _unitOfWork.Counties.GetByStateAsync(stateId, cancellationToken);
            var foundCounty = counties.FirstOrDefault(c => 
                string.Equals(c.Name, county, StringComparison.OrdinalIgnoreCase));
            
            if (foundCounty != null)
            {
                countyId = foundCounty.Id;
            }
            else
            {
                _logger.LogWarning($"Could not find county: {county} in state {stateId}");
            }
        }

        // Create new water body
        var newWaterBody = new WaterBody
        {
            Name = lakeName.Trim(),
            StateId = stateId,
            CountyId = countyId,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createdWaterBody = await _unitOfWork.WaterBodies.AddAsync(newWaterBody, cancellationToken);
        _logger.LogInformation($"Created new water body: {lakeName}");
        
        return createdWaterBody;
    }

    public async Task<Dictionary<string, FishSpecies>> FindOrCreateFishSpeciesAsync(
        IEnumerable<string> speciesNames,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, FishSpecies>(StringComparer.OrdinalIgnoreCase);

        foreach (var speciesName in speciesNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
        {
            var normalizedName = NormalizeFishSpeciesName(speciesName);
            
            // Try to find existing species
            var existingSpecies = await _unitOfWork.FishSpecies.SearchByNameAsync(normalizedName, cancellationToken);
            var foundSpecies = existingSpecies.FirstOrDefault(fs => 
                string.Equals(fs.CommonName, normalizedName, StringComparison.OrdinalIgnoreCase));

            if (foundSpecies != null)
            {
                result[speciesName] = foundSpecies;
                continue;
            }

            // Create new species if not found
            var newSpecies = new FishSpecies
            {
                CommonName = normalizedName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var createdSpecies = await _unitOfWork.FishSpecies.AddAsync(newSpecies, cancellationToken);
            result[speciesName] = createdSpecies;
            
            _logger.LogInformation($"Created new fish species: {normalizedName}");
        }

        return result;
    }

    public RegulationValidationResult ValidateAndCleanRegulation(AiSpecialRegulation regulation)
    {
        var result = new RegulationValidationResult
        {
            CleanedRegulation = new AiSpecialRegulation
            {
                Species = regulation.Species?.Trim() ?? string.Empty,
                RegulationType = regulation.RegulationType,
                DailyLimit = regulation.DailyLimit,
                PossessionLimit = regulation.PossessionLimit,
                MinimumSize = CleanSizeString(regulation.MinimumSize),
                MaximumSize = CleanSizeString(regulation.MaximumSize),
                ProtectedSlot = CleanSizeString(regulation.ProtectedSlot),
                SeasonInfo = regulation.SeasonInfo?.Trim(),
                CatchAndRelease = regulation.CatchAndRelease,
                Notes = regulation.Notes?.Trim() ?? string.Empty
            }
        };

        // Validate species name
        if (string.IsNullOrWhiteSpace(result.CleanedRegulation.Species))
        {
            result.ValidationErrors.Add("Species name is required");
        }

        // Validate limits
        if (result.CleanedRegulation.DailyLimit < 0)
        {
            result.ValidationWarnings.Add($"Daily limit is negative: {result.CleanedRegulation.DailyLimit}");
        }

        if (result.CleanedRegulation.PossessionLimit < 0)
        {
            result.ValidationWarnings.Add($"Possession limit is negative: {result.CleanedRegulation.PossessionLimit}");
        }

        if (result.CleanedRegulation.DailyLimit > result.CleanedRegulation.PossessionLimit && 
            result.CleanedRegulation.PossessionLimit > 0)
        {
            result.ValidationWarnings.Add("Daily limit exceeds possession limit");
        }

        result.IsValid = result.ValidationErrors.Count == 0;
        return result;
    }

    private string NormalizeFishSpeciesName(string speciesName)
    {
        if (string.IsNullOrWhiteSpace(speciesName))
            return string.Empty;

        var normalized = speciesName.Trim();
        
        // Check if we have a known mapping
        if (SpeciesNameMappings.TryGetValue(normalized, out var mappedName))
        {
            return mappedName;
        }

        // Apply basic normalization
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized.ToLower());
    }

    private string? CleanSizeString(string? sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
            return null;

        // Remove extra whitespace and normalize
        return Regex.Replace(sizeString.Trim(), @"\s+", " ");
    }

    private FishingRegulation CreateFishingRegulationFromAi(
        AiSpecialRegulation aiRegulation,
        int waterBodyId,
        int speciesId,
        Guid sourceDocumentId,
        int regulationYear)
    {
        var now = DateTimeOffset.UtcNow;
        var effectiveDate = new DateOnly(regulationYear, 1, 1);
        var expirationDate = new DateOnly(regulationYear, 12, 31);

        var regulation = new FishingRegulation
        {
            Id = Guid.NewGuid(),
            WaterBodyId = waterBodyId,
            SpeciesId = speciesId,
            RegulationYear = regulationYear,
            SourceDocumentId = sourceDocumentId,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            
            // Limits
            DailyLimit = aiRegulation.DailyLimit,
            PossessionLimit = aiRegulation.PossessionLimit,
            
            // Sizes (extract numeric values where possible)
            MinimumSizeInches = ExtractSizeInInches(aiRegulation.MinimumSize),
            MaximumSizeInches = ExtractSizeInInches(aiRegulation.MaximumSize),
            SizeLimitNotes = string.Join("; ", new[] { aiRegulation.MinimumSize, aiRegulation.MaximumSize, aiRegulation.ProtectedSlot }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            
            // Special regulations
            SpecialRegulations = new List<string> { aiRegulation.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            
            // Season info
            SeasonNotes = aiRegulation.SeasonInfo,
            IsYearRound = string.IsNullOrWhiteSpace(aiRegulation.SeasonInfo),
            
            // Metadata
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Extract protected slot information
        ExtractProtectedSlotInfo(aiRegulation.ProtectedSlot, regulation);

        return regulation;
    }

    private void UpdateFishingRegulationFromAi(FishingRegulation existing, AiSpecialRegulation aiRegulation)
    {
        existing.DailyLimit = aiRegulation.DailyLimit;
        existing.PossessionLimit = aiRegulation.PossessionLimit;
        existing.MinimumSizeInches = ExtractSizeInInches(aiRegulation.MinimumSize);
        existing.MaximumSizeInches = ExtractSizeInInches(aiRegulation.MaximumSize);
        existing.SizeLimitNotes = string.Join("; ", new[] { aiRegulation.MinimumSize, aiRegulation.MaximumSize, aiRegulation.ProtectedSlot }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        existing.SpecialRegulations = new List<string> { aiRegulation.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        existing.SeasonNotes = aiRegulation.SeasonInfo;
        existing.IsYearRound = string.IsNullOrWhiteSpace(aiRegulation.SeasonInfo);
        
        ExtractProtectedSlotInfo(aiRegulation.ProtectedSlot, existing);
    }

    private decimal? ExtractSizeInInches(string? sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
            return null;

        // Look for patterns like "15 inches", "15", "15.5 in", etc.
        var match = Regex.Match(sizeString, @"(\d+(?:\.\d+)?)\s*(?:inch|inches|in)?", RegexOptions.IgnoreCase);
        
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var size))
        {
            return size;
        }

        return null;
    }

    private void ExtractProtectedSlotInfo(string? protectedSlotString, FishingRegulation regulation)
    {
        if (string.IsNullOrWhiteSpace(protectedSlotString))
            return;

        // Look for patterns like "28-36 inches (1 fish allowed)" or "20-24 inches"
        var slotMatch = Regex.Match(protectedSlotString, @"(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)\s*(?:inch|inches|in)?", RegexOptions.IgnoreCase);
        
        if (slotMatch.Success)
        {
            if (decimal.TryParse(slotMatch.Groups[1].Value, out var minSize))
                regulation.ProtectedSlotMinInches = minSize;
            
            if (decimal.TryParse(slotMatch.Groups[2].Value, out var maxSize))
                regulation.ProtectedSlotMaxInches = maxSize;
        }

        // Look for exception numbers like "(1 fish allowed)"
        var exceptionMatch = Regex.Match(protectedSlotString, @"\((\d+)\s+fish", RegexOptions.IgnoreCase);
        if (exceptionMatch.Success && int.TryParse(exceptionMatch.Groups[1].Value, out var exceptions))
        {
            regulation.ProtectedSlotExceptions = exceptions;
        }
    }
}
