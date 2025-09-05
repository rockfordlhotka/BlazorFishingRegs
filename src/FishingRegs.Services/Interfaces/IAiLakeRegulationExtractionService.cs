using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Service for extracting lake-specific fishing regulations using AI processing
/// </summary>
public interface IAiLakeRegulationExtractionService
{
    /// <summary>
    /// Extracts lake regulations from the fishing regulations text using AI processing
    /// </summary>
    /// <param name="regulationsText">The full text of the fishing regulations document</param>
    /// <returns>Extracted lake regulations</returns>
    Task<AiLakeRegulationExtractionResult> ExtractLakeRegulationsAsync(string regulationsText);

    /// <summary>
    /// Extracts regulations for a specific section of text (e.g., a particular lake entry)
    /// </summary>
    /// <param name="lakeText">Text containing a specific lake's regulations</param>
    /// <param name="lakeName">Name of the lake</param>
    /// <param name="county">County where the lake is located</param>
    /// <returns>Structured regulation data for the lake</returns>
    Task<AiLakeRegulation?> ExtractSingleLakeRegulationAsync(string lakeText, string lakeName, string county = "");

    /// <summary>
    /// Extracts and processes lake regulations one at a time, calling the provided callback for each completed lake
    /// This allows for real-time processing where each lake's database update happens immediately after AI extraction
    /// </summary>
    /// <param name="regulationsText">The full text of the fishing regulations document</param>
    /// <param name="onLakeProcessed">Callback function called for each successfully extracted lake regulation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of the extraction process</returns>
    Task<AiLakeRegulationExtractionResult> ExtractLakeRegulationsStreamAsync(
        string regulationsText,
        Func<AiLakeRegulation, Task> onLakeProcessed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes text and splits it into individual lake regulation entries
    /// </summary>
    /// <param name="specialRegulationsSection">The "Waters With Experimental and Special Regulations" section text</param>
    /// <returns>List of individual lake regulation text blocks</returns>
    List<(string LakeName, string County, string RegulationText)> ParseLakeEntries(string specialRegulationsSection);
}
