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

    // In-memory cache to prevent duplicate species creation within the same processing session
    private readonly Dictionary<string, FishSpecies> _sessionSpeciesCache = new(StringComparer.OrdinalIgnoreCase);

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

            // Clear session cache for new processing run
            _sessionSpeciesCache.Clear();

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

            // Save all changes at the end for batch processing
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
            _sessionSpeciesCache.Clear(); // Clean up cache
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
            // Find or create the water body - AI should provide clean lake names
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

            // If this is a new water body, save it now before creating regulations
            if (result.WaterBody.Id == 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogDebug($"Saved new water body {lakeRegulation.LakeName} with ID {result.WaterBody.Id}");
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

            // Find or create fish species - AI should provide standardized names
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

                // Create fishing regulation record using the cleaned regulation
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
        catch (ArgumentException ex) when (ex.Message.Contains("Lake name cannot be empty"))
        {
            _logger.LogWarning(ex, $"Invalid lake name: {lakeRegulation.LakeName}");
            result.ErrorMessage = $"Invalid lake name: {ex.Message}";
            result.Warnings.Add($"Skipped processing due to invalid lake name: {lakeRegulation.LakeName}");
        }
        catch (Exception ex) when (IsStringTooLongException(ex))
        {
            _logger.LogWarning(ex, $"Data too long for database constraints while processing lake: {lakeRegulation.LakeName}");
            result.ErrorMessage = $"Data validation error: Some extracted data exceeds database field limits";
            result.Warnings.Add($"Skipped lake due to data length constraints: {lakeRegulation.LakeName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing lake: {lakeRegulation.LakeName}");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Checks if an exception is related to string length constraints
    /// </summary>
    private static bool IsStringTooLongException(Exception ex)
    {
        return ex.Message.Contains("value too long for type", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("22001") ||
               (ex.InnerException != null && IsStringTooLongException(ex.InnerException));
    }

    public async Task<WaterBody> FindOrCreateWaterBodyAsync(
        string lakeName,
        string county,
        int stateId = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lakeName))
            throw new ArgumentException("Lake name cannot be empty", nameof(lakeName));

        // AI should now provide clean lake names, so minimal cleaning needed
        var cleanedLakeName = lakeName.Trim();
        
        if (cleanedLakeName.Length > 200)
        {
            _logger.LogWarning($"Lake name too long, truncating: {cleanedLakeName}");
            cleanedLakeName = TruncateAtWordBoundary(cleanedLakeName, 200);
        }

        // First, try to find existing water body
        var existingWaterBodies = await _unitOfWork.WaterBodies.SearchByNameAsync(cleanedLakeName, cancellationToken);
        var existingWaterBody = existingWaterBodies.FirstOrDefault(wb => 
            wb.StateId == stateId && 
            string.Equals(wb.Name, cleanedLakeName, StringComparison.OrdinalIgnoreCase));

        if (existingWaterBody != null)
        {
            _logger.LogDebug($"Found existing water body: {cleanedLakeName}");
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
            Name = cleanedLakeName,
            StateId = stateId,
            CountyId = countyId,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createdWaterBody = await _unitOfWork.WaterBodies.AddAsync(newWaterBody, cancellationToken);
        _logger.LogInformation($"Created new water body: {cleanedLakeName}");
        
        return createdWaterBody;
    }

    /// <summary>
    /// Truncates text at word boundary if possible
    /// </summary>
    private string TruncateAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        var truncated = text.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        if (lastSpace > maxLength / 2) // Only truncate at word boundary if reasonable
        {
            truncated = truncated.Substring(0, lastSpace);
        }
        
        return truncated.Trim();
    }

    public async Task<Dictionary<string, FishSpecies>> FindOrCreateFishSpeciesAsync(
        IEnumerable<string> speciesNames,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, FishSpecies>(StringComparer.OrdinalIgnoreCase);

        foreach (var originalSpeciesName in speciesNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
        {
            // AI should provide standardized names, so minimal normalization needed
            var normalizedName = originalSpeciesName.Trim();
            
            // Check session cache first
            if (_sessionSpeciesCache.TryGetValue(normalizedName, out var cachedSpecies))
            {
                result[originalSpeciesName] = cachedSpecies;
                continue;
            }

            // Try to find existing species in database
            var allSpecies = await _unitOfWork.FishSpecies.GetAllAsync(cancellationToken);
            var foundSpecies = allSpecies.FirstOrDefault(fs => 
                string.Equals(fs.CommonName.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

            if (foundSpecies != null)
            {
                result[originalSpeciesName] = foundSpecies;
                _sessionSpeciesCache[normalizedName] = foundSpecies;
                continue;
            }

            // If not found by exact match, try the search method as a fallback
            var searchResults = await _unitOfWork.FishSpecies.SearchByNameAsync(normalizedName, cancellationToken);
            var searchedSpecies = searchResults.FirstOrDefault(fs => 
                string.Equals(fs.CommonName.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

            if (searchedSpecies != null)
            {
                result[originalSpeciesName] = searchedSpecies;
                _sessionSpeciesCache[normalizedName] = searchedSpecies;
                continue;
            }

            try
            {
                // Create new species if not found
                var newSpecies = new FishSpecies
                {
                    CommonName = normalizedName,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                var createdSpecies = await _unitOfWork.FishSpecies.AddAsync(newSpecies, cancellationToken);
                
                // Save immediately and handle race conditions
                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    result[originalSpeciesName] = createdSpecies;
                    _sessionSpeciesCache[normalizedName] = createdSpecies;
                    _logger.LogInformation($"Created new fish species: {normalizedName}");
                }
                catch (Exception saveEx) when (IsDuplicateKeyException(saveEx))
                {
                    // Handle race condition
                    _logger.LogDebug($"Species {normalizedName} was created by another process, retrying search");
                    
                    var retrySpecies = await _unitOfWork.FishSpecies.GetAllAsync(cancellationToken);
                    var foundAfterRetry = retrySpecies.FirstOrDefault(fs => 
                        string.Equals(fs.CommonName.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));
                    
                    if (foundAfterRetry != null)
                    {
                        result[originalSpeciesName] = foundAfterRetry;
                        _sessionSpeciesCache[normalizedName] = foundAfterRetry;
                        _logger.LogDebug($"Successfully found species {normalizedName} after duplicate key resolution");
                    }
                    else
                    {
                        _logger.LogError($"Failed to find species {normalizedName} after duplicate key error resolution");
                        throw new InvalidOperationException($"Unable to find or create fish species: {normalizedName}");
                    }
                }
            }
            catch (Exception ex) when (!IsDuplicateKeyException(ex))
            {
                _logger.LogError(ex, $"Unexpected error creating fish species: {normalizedName}");
                throw;
            }
        }

        return result;
    }

    private static bool IsDuplicateKeyException(Exception ex)
    {
        return ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) || 
               ex.Message.Contains("23505") ||
               (ex.InnerException != null && IsDuplicateKeyException(ex.InnerException));
    }

    public RegulationValidationResult ValidateAndCleanRegulation(AiSpecialRegulation regulation)
    {
        var result = new RegulationValidationResult
        {
            CleanedRegulation = regulation // AI should provide clean data already
        };

        // Basic validation only - AI should provide good data
        if (string.IsNullOrWhiteSpace(regulation.Species))
        {
            result.ValidationErrors.Add("Species name is required");
        }

        if (regulation.DailyLimit < 0)
        {
            result.ValidationWarnings.Add($"Daily limit is negative: {regulation.DailyLimit}");
        }

        if (regulation.PossessionLimit < 0)
        {
            result.ValidationWarnings.Add($"Possession limit is negative: {regulation.PossessionLimit}");
        }

        if (regulation.DailyLimit > regulation.PossessionLimit && 
            regulation.PossessionLimit > 0)
        {
            result.ValidationWarnings.Add("Daily limit exceeds possession limit");
        }

        result.IsValid = result.ValidationErrors.Count == 0;
        return result;
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
            WaterBodyId = waterBodyId,
            SpeciesId = speciesId,
            RegulationYear = regulationYear,
            SourceDocumentId = sourceDocumentId,
            RegulationType = "general",
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            
            // Limits - AI should provide clean numeric values
            DailyLimit = aiRegulation.DailyLimit,
            PossessionLimit = aiRegulation.PossessionLimit,
            
            // Sizes - AI should provide clean size strings with units
            MinimumSizeInches = ExtractSizeInInches(aiRegulation.MinimumSize),
            MaximumSizeInches = ExtractSizeInInches(aiRegulation.MaximumSize),
            
            // Special regulations
            SpecialRegulations = new List<string> { aiRegulation.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            
            // Season info
            Notes = aiRegulation.SeasonInfo,
            
            // Catch and release flag
            IsCatchAndRelease = aiRegulation.CatchAndRelease,
            
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
        existing.SpecialRegulations = new List<string> { aiRegulation.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        existing.Notes = aiRegulation.SeasonInfo;
        existing.IsCatchAndRelease = aiRegulation.CatchAndRelease;
        
        ExtractProtectedSlotInfo(aiRegulation.ProtectedSlot, existing);
    }

    private decimal? ExtractSizeInInches(string? sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
            return null;

        // AI should provide clean size strings like "15 inches", but still need basic parsing
        var match = Regex.Match(sizeString, @"(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        
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

        // AI should provide clean slot ranges like "28-36 inches"
        var slotMatch = Regex.Match(protectedSlotString, @"(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        
        if (slotMatch.Success)
        {
            if (decimal.TryParse(slotMatch.Groups[1].Value, out var minSize))
                regulation.ProtectedSlotMinInches = minSize;
            
            if (decimal.TryParse(slotMatch.Groups[2].Value, out var maxSize))
                regulation.ProtectedSlotMaxInches = maxSize;
        }
    }
}
