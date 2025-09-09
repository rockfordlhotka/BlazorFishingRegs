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
        { "stream trout", "Brook Trout" }, // Fix for Stream Trout issue
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

            // IMPORTANT: If this is a new water body (Id = 0), save it now before creating regulations
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
                    _logger.LogDebug($"Updated existing regulation for {fishSpecies.CommonName} in {result.WaterBody.Name}");
                }
                else
                {
                    // Double-check: Also verify this regulation isn't already in our current batch
                    var alreadyInBatch = result.CreatedRegulations.Any(cr => 
                        cr.WaterBodyId == result.WaterBody.Id && 
                        cr.SpeciesId == fishSpecies.Id && 
                        cr.RegulationYear == regulationYear);

                    if (!alreadyInBatch)
                    {
                        try
                        {
                            // Add new regulation with individual duplicate key handling
                            await _unitOfWork.FishingRegulations.AddAsync(fishingRegulation, cancellationToken);
                            result.CreatedRegulations.Add(fishingRegulation);
                            _logger.LogDebug($"Added new regulation for {fishSpecies.CommonName} in {result.WaterBody.Name}");
                        }
                        catch (Exception ex) when (IsDuplicateKeyException(ex))
                        {
                            _logger.LogDebug($"Duplicate regulation detected during add for {fishSpecies.CommonName} in {result.WaterBody.Name}, skipping");
                            // Continue processing other regulations without failing
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"Regulation for {fishSpecies.CommonName} in {result.WaterBody.Name} already in current batch, skipping");
                    }
                }
            }

            result.IsSuccess = true;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Could not extract valid lake name"))
        {
            _logger.LogWarning(ex, $"Invalid lake name format: {lakeRegulation.LakeName}");
            result.ErrorMessage = $"Invalid lake name format: {ex.Message}";
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

        // Clean and validate the lake name
        var cleanedLakeName = CleanLakeName(lakeName);
        
        if (string.IsNullOrWhiteSpace(cleanedLakeName))
        {
            _logger.LogWarning($"Could not extract valid lake name from: {lakeName}");
            throw new ArgumentException($"Could not extract valid lake name from: {lakeName}", nameof(lakeName));
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
    /// Cleans and extracts the actual lake name from AI-extracted text that may contain regulations
    /// </summary>
    private string CleanLakeName(string lakeName)
    {
        if (string.IsNullOrWhiteSpace(lakeName))
            return string.Empty;

        var originalName = lakeName.Trim();
        
        // If the name is within the 200 character limit and looks like a proper lake name, return it as-is
        if (originalName.Length <= 200 && !ContainsRegulationText(originalName))
        {
            return originalName;
        }

        _logger.LogWarning($"Lake name needs cleaning ({originalName.Length} chars): {originalName}");

        // Strategy 1: Look for lake names in ALL CAPS (common pattern in regulation documents)
        var upperCaseMatches = Regex.Matches(originalName, @"\b[A-Z][A-Z\s]+LAKE\b");
        if (upperCaseMatches.Count > 0)
        {
            var extractedName = upperCaseMatches[0].Value.Trim();
            if (extractedName.Length <= 200)
            {
                _logger.LogInformation($"Extracted lake name from caps: {extractedName}");
                return extractedName;
            }
        }

        // Strategy 2: Look for patterns like "LAKE NAME including..." or "LAKE NAME and outlet..."
        var includesMatch = Regex.Match(originalName, @"([A-Z][A-Z\s]+LAKE)(?:\s+(?:including|and|plus|with))", RegexOptions.IgnoreCase);
        if (includesMatch.Success)
        {
            var extractedName = includesMatch.Groups[1].Value.Trim();
            if (extractedName.Length <= 200)
            {
                _logger.LogInformation($"Extracted lake name from 'including' pattern: {extractedName}");
                return extractedName;
            }
        }

        // Strategy 3: Look for any mention of "LAKE" and extract surrounding context
        var lakeMatch = Regex.Match(originalName, @"([A-Z][A-Z\s]*LAKE[A-Z\s]*)", RegexOptions.IgnoreCase);
        if (lakeMatch.Success)
        {
            var extractedName = lakeMatch.Groups[1].Value.Trim();
            if (extractedName.Length <= 200)
            {
                _logger.LogInformation($"Extracted lake name from 'LAKE' pattern: {extractedName}");
                return extractedName;
            }
        }

        // Strategy 4: If text contains regulation keywords, try to extract just the lake name
        if (ContainsRegulationText(originalName))
        {
            var cleanedName = ExtractLakeNameFromRegulationText(originalName);
            if (!string.IsNullOrEmpty(cleanedName) && cleanedName.Length <= 200)
            {
                _logger.LogInformation($"Extracted lake name from regulation text: {cleanedName}");
                return cleanedName;
            }
        }

        // Strategy 5: As a last resort, truncate to 200 characters at a word boundary
        if (originalName.Length > 200)
        {
            var truncated = originalName.Substring(0, 200);
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > 100) // Only truncate at word boundary if we have reasonable length
            {
                truncated = truncated.Substring(0, lastSpace);
            }
            
            _logger.LogWarning($"Truncated lake name to: {truncated}");
            return truncated.Trim();
        }

        return originalName;
    }

    /// <summary>
    /// Checks if the text contains regulation keywords that indicate it's not just a lake name
    /// </summary>
    private bool ContainsRegulationText(string text)
    {
        var regulationKeywords = new[]
        {
            "bass:", "pike:", "trout:", "salmon:", "walleye:", "muskie:", "perch:", "crappie:",
            "catch-and-release", "possession limit", "daily limit", "size limit",
            "must be released", "immediately released", "over", "under",
            "inches", "feet", "pounds"
        };

        return regulationKeywords.Any(keyword => 
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Attempts to extract a lake name from text that contains both lake name and regulation content
    /// </summary>
    private string ExtractLakeNameFromRegulationText(string text)
    {
        try
        {
            // Look for patterns where lake names appear at the end of regulation text
            // Example: "bass: catch-and-release only. Northern pike: ... ANNIE BATTLE LAKE including inlet"
            var endLakePattern = @"([A-Z][A-Z\s]+LAKE)(?:\s+(?:including|and|near).*)?$";
            var match = Regex.Match(text, endLakePattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Look for lake names that appear before regulation keywords
            var beforeRegulationPattern = @"([A-Z][A-Z\s]+LAKE)(?:\s+(?:including[^:]*)?)\s*(?:.*?(?:bass|pike|trout|salmon|walleye|muskie|perch|crappie):)";
            match = Regex.Match(text, beforeRegulationPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Look for any capitalized lake name in the text
            var anyLakePattern = @"\b([A-Z][A-Z\s]+LAKE)\b";
            match = Regex.Match(text, anyLakePattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting lake name from regulation text");
        }

        return "";
    }

    public async Task<Dictionary<string, FishSpecies>> FindOrCreateFishSpeciesAsync(
        IEnumerable<string> speciesNames,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, FishSpecies>(StringComparer.OrdinalIgnoreCase);

        foreach (var originalSpeciesName in speciesNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
        {
            var normalizedName = NormalizeFishSpeciesName(originalSpeciesName);
            
            // Check session cache first to avoid duplicate creation within this processing session
            if (_sessionSpeciesCache.TryGetValue(normalizedName, out var cachedSpecies))
            {
                result[originalSpeciesName] = cachedSpecies;
                continue;
            }

            // Try to find existing species in database with exact match on normalized name
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
                
                // Important: Save immediately and retry on duplicate to handle race conditions
                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    result[originalSpeciesName] = createdSpecies;
                    _sessionSpeciesCache[normalizedName] = createdSpecies;
                    _logger.LogInformation($"Created new fish species: {normalizedName}");
                }
                catch (Exception saveEx) when (IsDuplicateKeyException(saveEx))
                {
                    // Handle race condition - another process created the species while we were processing
                    _logger.LogDebug($"Species {normalizedName} was created by another process during save, retrying search");
                    
                    // Reload the species that was created by another process
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
            WaterBodyId = waterBodyId,
            SpeciesId = speciesId,
            RegulationYear = regulationYear,
            SourceDocumentId = sourceDocumentId,
            RegulationType = "general", // Default regulation type
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            
            // Limits
            DailyLimit = aiRegulation.DailyLimit,
            PossessionLimit = aiRegulation.PossessionLimit,
            
            // Sizes (extract numeric values where possible)
            MinimumSizeInches = ExtractSizeInInches(aiRegulation.MinimumSize),
            MaximumSizeInches = ExtractSizeInInches(aiRegulation.MaximumSize),
            
            // Special regulations
            SpecialRegulations = new List<string> { aiRegulation.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            
            // Store season info in the general notes field since there's no season_notes column
            Notes = aiRegulation.SeasonInfo,
            
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
        existing.Notes = aiRegulation.SeasonInfo; // Store season info in general notes field
        
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

        // Note: Protected slot exceptions are noted in the special regulations instead
    }
}
