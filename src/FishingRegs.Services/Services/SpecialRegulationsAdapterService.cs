using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Logging;

namespace FishingRegs.Services.Services;

/// <summary>
/// Adapter service that converts parsed special regulations to AI extraction format
/// </summary>
public class SpecialRegulationsAdapterService
{
    private readonly ILogger<SpecialRegulationsAdapterService> _logger;

    public SpecialRegulationsAdapterService(ILogger<SpecialRegulationsAdapterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts parsed special regulations to AI extraction result format
    /// </summary>
    public AiLakeRegulationExtractionResult ConvertToAiExtractionResult(SpecialRegulationsParseResult parseResult)
    {
        var result = new AiLakeRegulationExtractionResult
        {
            IsSuccess = parseResult.IsSuccess,
            TotalLakesProcessed = parseResult.TotalLakesParsed,
            TotalRegulationsExtracted = parseResult.TotalSpeciesRegulationsParsed,
            ProcessingTime = parseResult.ProcessingTime,
            ErrorMessage = parseResult.ErrorMessage ?? string.Empty
        };

        foreach (var parsedLake in parseResult.ParsedLakes)
        {
            var aiLakeRegulation = ConvertToAiLakeRegulation(parsedLake);
            result.ExtractedRegulations.Add(aiLakeRegulation);
        }

        result.ProcessingWarnings.AddRange(parseResult.ParseWarnings);

        _logger.LogInformation($"Converted {result.ExtractedRegulations.Count} parsed lakes to AI format");

        return result;
    }

    private AiLakeRegulation ConvertToAiLakeRegulation(ParsedLakeEntry parsedLake)
    {
        var aiLakeRegulation = new AiLakeRegulation
        {
            LakeName = parsedLake.LakeName,
            County = parsedLake.County
        };

        aiLakeRegulation.Regulations.SpecialRegulations = parsedLake.SpeciesRegulations
            .Select(ConvertToAiSpecialRegulation)
            .ToList();

        aiLakeRegulation.Regulations.LastUpdated = DateTime.UtcNow;
        aiLakeRegulation.Regulations.GeneralNotes = parsedLake.RegulationText;

        return aiLakeRegulation;
    }

    private AiSpecialRegulation ConvertToAiSpecialRegulation(ParsedSpeciesRegulation parsedRegulation)
    {
        return new AiSpecialRegulation
        {
            Species = parsedRegulation.Species,
            RegulationType = MapRegulationType(parsedRegulation.RegulationType),
            DailyLimit = parsedRegulation.DailyLimit,
            PossessionLimit = parsedRegulation.PossessionLimit,
            MinimumSize = parsedRegulation.MinimumSize,
            MaximumSize = parsedRegulation.MaximumSize,
            ProtectedSlot = parsedRegulation.ProtectedSlot,
            CatchAndRelease = parsedRegulation.IsCatchAndRelease,
            Notes = parsedRegulation.RegulationDetails
        };
    }

    private AiRegulationType MapRegulationType(string? regulationType)
    {
        return regulationType?.ToLower() switch
        {
            "dailylimit" => AiRegulationType.DailyLimit,
            "possessionlimit" => AiRegulationType.PossessionLimit,
            "sizelimit" => AiRegulationType.SizeLimit,
            "protectedslot" => AiRegulationType.ProtectedSlot,
            "catchandrelease" => AiRegulationType.CatchAndRelease,
            "seasonal" => AiRegulationType.Seasonal,
            "combined" => AiRegulationType.Combined,
            _ => AiRegulationType.Combined
        };
    }
}
