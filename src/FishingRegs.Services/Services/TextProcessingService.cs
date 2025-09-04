using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FishingRegs.Services.Services;

/// <summary>
/// Text processing service implementation for fishing regulations
/// </summary>
public class TextProcessingService : ITextProcessingService
{
    private readonly IAiLakeRegulationExtractionService _aiExtractionService;
    private readonly ITextChunkingService _textChunkingService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TextProcessingService> _logger;

    // In-memory store for processing documents (in production, use a database)
    private readonly Dictionary<Guid, ProcessingDocument> _processingDocuments = new();

    public TextProcessingService(
        IAiLakeRegulationExtractionService aiExtractionService,
        ITextChunkingService textChunkingService,
        IBlobStorageService blobStorageService,
        IConfiguration configuration,
        ILogger<TextProcessingService> logger)
    {
        _aiExtractionService = aiExtractionService;
        _textChunkingService = textChunkingService;
        _blobStorageService = blobStorageService;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> ValidateTextAsync(string fileName, string textContent)
    {
        try
        {
            _logger.LogInformation("Validating text content for file: {FileName}", fileName);

            // Check if content is not empty
            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Text content is empty for file: {FileName}", fileName);
                return Task.FromResult(false);
            }

            // Check minimum length
            if (textContent.Length < 100)
            {
                _logger.LogWarning("Text content too short for file: {FileName} (Length: {Length})", 
                    fileName, textContent.Length);
                return Task.FromResult(false);
            }

            // Check for suspicious content patterns
            var suspiciousPatterns = new[]
            {
                @"^[^a-zA-Z]*$", // Only non-alphabetic characters
                @"^\s*$" // Only whitespace
            };

            foreach (var pattern in suspiciousPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(textContent, pattern))
                {
                    _logger.LogWarning("Text content contains suspicious patterns for file: {FileName}", fileName);
                    return Task.FromResult(false);
                }
            }

            _logger.LogInformation("Text validation successful for file: {FileName}", fileName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating text content for file: {FileName}", fileName);
            return Task.FromResult(false);
        }
    }

    public async Task<ProcessingDocument> ProcessTextAsync(
        string textContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var processingDoc = new ProcessingDocument
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            Status = DocumentProcessingStatus.Started,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting text processing for file: {FileName} (ID: {DocumentId})", 
                fileName, processingDoc.Id);

            // Store processing document
            _processingDocuments[processingDoc.Id] = processingDoc;

            // Step 1: Validate text content
            if (!await ValidateTextAsync(fileName, textContent))
            {
                processingDoc.Status = DocumentProcessingStatus.Failed;
                processingDoc.ErrorMessage = "Text validation failed";
                processingDoc.LastUpdatedAt = DateTime.UtcNow;
                return processingDoc;
            }

            processingDoc.Status = DocumentProcessingStatus.InProgress;
            processingDoc.LastUpdatedAt = DateTime.UtcNow;

            // Step 2: Optionally store text content in blob storage for archival
            try
            {
                using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));
                var uploadResult = await _blobStorageService.UploadDocumentAsync(
                    textStream, fileName, "text/plain", cancellationToken);

                processingDoc.BlobUrl = uploadResult.BlobUrl;
                processingDoc.BlobName = uploadResult.BlobName;

                _logger.LogInformation("Uploaded text content for {FileName} to blob storage as {BlobName}",
                    fileName, uploadResult.BlobName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload text to blob storage for {FileName}, continuing without storage", fileName);
            }

            // Step 3: Process text with AI extraction
            _logger.LogInformation("Processing text with AI extraction: {FileName}", fileName);

            // Chunk the text for better processing
            var chunkingResult = _textChunkingService.ChunkTextIntelligently(textContent);
            
            if (!chunkingResult.IsSuccess)
            {
                throw new InvalidOperationException($"Text chunking failed: {chunkingResult.ErrorMessage}");
            }

            // Filter for fishing-related content
            var filteredChunks = _textChunkingService.FilterFishingChunks(chunkingResult);

            // Extract fishing regulation data using AI
            var fishingRegulationData = await ExtractFishingRegulationDataAsync(textContent, fileName);

            // Step 4: Store results
            processingDoc.FishingRegulationData = fishingRegulationData;
            processingDoc.Status = DocumentProcessingStatus.Completed;
            processingDoc.LastUpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Text processing completed successfully for {FileName} (ID: {DocumentId})", 
                fileName, processingDoc.Id);

            return processingDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing text for file: {FileName} (ID: {DocumentId})", 
                fileName, processingDoc.Id);

            processingDoc.Status = DocumentProcessingStatus.Failed;
            processingDoc.ErrorMessage = ex.Message;
            processingDoc.LastUpdatedAt = DateTime.UtcNow;

            return processingDoc;
        }
    }

    public Task<ProcessingDocument?> GetProcessingStatusAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_processingDocuments.GetValueOrDefault(documentId));
    }

    public async Task<FishingRegulationData> ExtractFishingRegulationDataAsync(
        string textContent,
        string fileName)
    {
        try
        {
            _logger.LogInformation("Extracting fishing regulation data from text for file: {FileName}", fileName);

            // Use the AI extraction service to process the text
            var extractionResult = await _aiExtractionService.ExtractLakeRegulationsAsync(textContent);

            if (!extractionResult.IsSuccess)
            {
                throw new InvalidOperationException($"AI extraction failed: {extractionResult.ErrorMessage}");
            }

            var fishingRegulationData = new FishingRegulationData
            {
                DocumentName = fileName,
                ProcessedAt = DateTime.UtcNow,
                LakeRegulations = ConvertAiLakeRegulations(extractionResult.ExtractedRegulations),
                IsSuccess = true,
                TotalLakesProcessed = extractionResult.TotalLakesProcessed,
                TotalRegulationsExtracted = extractionResult.TotalRegulationsExtracted
            };

            _logger.LogInformation("Successfully extracted {Count} lake regulations from {FileName}", 
                fishingRegulationData.LakeRegulations.Count, fileName);

            return fishingRegulationData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fishing regulation data from text for file: {FileName}", fileName);

            return new FishingRegulationData
            {
                DocumentName = fileName,
                ProcessedAt = DateTime.UtcNow,
                LakeRegulations = new List<LakeRegulation>(),
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Converts AI lake regulations to standard lake regulations
    /// </summary>
    private List<LakeRegulation> ConvertAiLakeRegulations(List<AiLakeRegulation> aiRegulations)
    {
        return aiRegulations.Select(aiReg => new LakeRegulation
        {
            LakeName = aiReg.LakeName,
            County = aiReg.County,
            State = "Minnesota", // Assuming Minnesota for now
            Species = aiReg.Regulations.SpecialRegulations.Select(ConvertSpecialRegulation).ToList(),
            SpecialRegulations = new List<string> { aiReg.Regulations.GeneralNotes },
            Confidence = 0.8, // Default confidence
            ExtractedAt = DateTime.UtcNow
        }).ToList();
    }

    /// <summary>
    /// Converts AI special regulation to species regulation
    /// </summary>
    private SpeciesRegulation ConvertSpecialRegulation(AiSpecialRegulation aiSpecialReg)
    {
        return new SpeciesRegulation
        {
            SpeciesName = aiSpecialReg.Species,
            DailyLimit = aiSpecialReg.DailyLimit,
            PossessionLimit = aiSpecialReg.PossessionLimit,
            MinimumSizeInches = ParseSizeValue(aiSpecialReg.MinimumSize),
            MaximumSizeInches = ParseSizeValue(aiSpecialReg.MaximumSize),
            SeasonInfo = aiSpecialReg.SeasonInfo,
            SizeRestrictions = aiSpecialReg.ProtectedSlot,
            IsCatchAndRelease = aiSpecialReg.CatchAndRelease,
            Confidence = 0.8, // Default confidence
            Notes = aiSpecialReg.Notes
        };
    }

    /// <summary>
    /// Parses size value from string to decimal
    /// </summary>
    private decimal? ParseSizeValue(string? sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
            return null;

        // Try to extract numeric value from size string (e.g., "12 inches" -> 12)
        var match = System.Text.RegularExpressions.Regex.Match(sizeString, @"(\d+(?:\.\d+)?)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var size))
            return size;

        return null;
    }
}
