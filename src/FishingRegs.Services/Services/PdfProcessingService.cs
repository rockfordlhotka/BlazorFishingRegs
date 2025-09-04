using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;

namespace FishingRegs.Services.Services;

/// <summary>
/// PDF processing service implementation for fishing regulations
/// </summary>
public class PdfProcessingService : IPdfProcessingService
{
    private readonly IAzureDocumentIntelligenceService _documentIntelligenceService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfSplittingService _pdfSplittingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfProcessingService> _logger;
    private readonly PdfValidationOptions _validationOptions;

    // In-memory store for processing documents (in production, use a database)
    private readonly Dictionary<Guid, ProcessingDocument> _processingDocuments = new();

    public PdfProcessingService(
        IAzureDocumentIntelligenceService documentIntelligenceService,
        IBlobStorageService blobStorageService,
        IPdfSplittingService pdfSplittingService,
        IConfiguration configuration,
        ILogger<PdfProcessingService> logger)
    {
        _documentIntelligenceService = documentIntelligenceService;
        _blobStorageService = blobStorageService;
        _pdfSplittingService = pdfSplittingService;
        _configuration = configuration;
        _logger = logger;

        // Load validation options from configuration
        _validationOptions = new PdfValidationOptions();
        configuration.GetSection("PdfValidation").Bind(_validationOptions);
    }

    public async Task<bool> ValidatePdfAsync(
        string fileName,
        string contentType,
        long fileSize,
        Stream stream)
    {
        try
        {
            _logger.LogInformation("Validating PDF file {FileName}", fileName);

            // Check file extension
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File {FileName} does not have PDF extension", fileName);
                return false;
            }

            // Check content type
            if (!_validationOptions.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File {FileName} has invalid content type: {ContentType}", fileName, contentType);
                return false;
            }

            // Check file size
            if (fileSize > _validationOptions.MaxFileSizeBytes)
            {
                _logger.LogWarning("File {FileName} exceeds maximum size limit: {FileSize} > {MaxSize}",
                    fileName, fileSize, _validationOptions.MaxFileSizeBytes);
                return false;
            }

            // Basic content validation - check for PDF header
            var originalPosition = stream.Position;
            stream.Position = 0;

            var buffer = new byte[8];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            stream.Position = originalPosition;

            var header = Encoding.ASCII.GetString(buffer);
            if (!header.StartsWith("%PDF"))
            {
                _logger.LogWarning("File {FileName} does not have valid PDF header", fileName);
                return false;
            }

            _logger.LogInformation("PDF file {FileName} passed validation", fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PDF file {FileName}", fileName);
            return false;
        }
    }

    public async Task<ProcessingDocument> ProcessPdfAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var processingDoc = new ProcessingDocument
        {
            FileName = fileName,
            ContentType = contentType,
            FileSize = stream.Length,
            UploadedAt = DateTime.UtcNow,
            Status = DocumentProcessingStatus.Pending
        };

        _processingDocuments[processingDoc.Id] = processingDoc;

        try
        {
            _logger.LogInformation("Starting PDF processing for {FileName} (ID: {DocumentId})",
                fileName, processingDoc.Id);

            // Step 1: Validate the PDF
            var isValid = await ValidatePdfAsync(fileName, contentType, stream.Length, stream);
            if (!isValid)
            {
                processingDoc.Status = DocumentProcessingStatus.Failed;
                processingDoc.ErrorMessage = "PDF validation failed";
                return processingDoc;
            }

            processingDoc.Status = DocumentProcessingStatus.InProgress;

            // Step 2: Upload to blob storage
            stream.Position = 0;
            var uploadResult = await _blobStorageService.UploadDocumentAsync(
                stream, fileName, contentType, cancellationToken);

            processingDoc.BlobUrl = uploadResult.BlobUrl;
            processingDoc.BlobName = uploadResult.BlobName;

            _logger.LogInformation("Uploaded {FileName} to blob storage as {BlobName}",
                fileName, uploadResult.BlobName);

            // Step 3: Analyze with Document Intelligence using intelligent splitting
            _logger.LogInformation("Processing document with intelligent splitting: {FileName}", fileName);
            stream.Position = 0; // Reset stream position
            
            var analysisResult = await _pdfSplittingService.ProcessSplitPdfAsync(stream, fileName, contentType);

            processingDoc.AnalysisResult = analysisResult;

            if (!analysisResult.IsSuccess)
            {
                processingDoc.Status = DocumentProcessingStatus.Failed;
                processingDoc.ErrorMessage = analysisResult.ErrorMessage ?? "Document analysis failed";
                return processingDoc;
            }

            // Step 4: Mark as completed
            processingDoc.Status = DocumentProcessingStatus.Completed;
            processingDoc.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation("Successfully processed PDF {FileName} (ID: {DocumentId})",
                fileName, processingDoc.Id);

            return processingDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF {FileName} (ID: {DocumentId})",
                fileName, processingDoc.Id);

            processingDoc.Status = DocumentProcessingStatus.Failed;
            processingDoc.ErrorMessage = ex.Message;
            return processingDoc;
        }
    }

    public async Task<ProcessingDocument?> GetProcessingStatusAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Placeholder for async pattern
        return _processingDocuments.TryGetValue(documentId, out var doc) ? doc : null;
    }

    public async Task<FishingRegulationData> ExtractFishingRegulationDataAsync(
        DocumentAnalysisResult analysisResult)
    {
        try
        {
            _logger.LogInformation("Extracting fishing regulation data from analysis result");

            var regulationData = new FishingRegulationData
            {
                OverallConfidence = analysisResult.ConfidenceScores.GetValueOrDefault("OverallConfidence", 0.0)
            };

            // Extract lake regulations from tables and text
            var lakes = ExtractLakeRegulations(analysisResult);
            regulationData.Lakes.AddRange(lakes);

            _logger.LogInformation("Extracted regulations for {LakeCount} lakes", lakes.Count);

            return await Task.FromResult(regulationData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fishing regulation data");
            throw;
        }
    }

    private List<LakeRegulation> ExtractLakeRegulations(DocumentAnalysisResult analysisResult)
    {
        var lakes = new List<LakeRegulation>();

        try
        {
            // Try to extract from tables first (more structured data)
            lakes.AddRange(ExtractFromTables(analysisResult.Tables));

            // If no tables or insufficient data, try text extraction
            if (lakes.Count == 0)
            {
                lakes.AddRange(ExtractFromText(analysisResult.ExtractedFields));
            }

            return lakes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting lake regulations");
            return lakes;
        }
    }

    private List<LakeRegulation> ExtractFromTables(List<ExtractedTable> tables)
    {
        var lakes = new List<LakeRegulation>();

        foreach (var table in tables)
        {
            try
            {
                // Look for tables with lake information
                if (table.Rows.Count <= 1) continue; // Skip tables without data rows

                var headerRow = table.Rows.FirstOrDefault();
                if (headerRow == null) continue;

                // Try to identify columns
                var lakeNameIndex = FindColumnIndex(headerRow, new[] { "lake", "water", "body" });
                var speciesIndex = FindColumnIndex(headerRow, new[] { "species", "fish" });
                var seasonIndex = FindColumnIndex(headerRow, new[] { "season", "date", "period" });
                var limitIndex = FindColumnIndex(headerRow, new[] { "limit", "bag", "size" });

                if (lakeNameIndex == -1) continue; // No lake column found

                // Process data rows
                for (int i = 1; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    if (row.Cells.Count <= lakeNameIndex) continue;

                    var lakeName = row.Cells[lakeNameIndex].Content?.Trim();
                    if (string.IsNullOrWhiteSpace(lakeName)) continue;

                    var existingLake = lakes.FirstOrDefault(l => 
                        string.Equals(l.LakeName, lakeName, StringComparison.OrdinalIgnoreCase));

                    if (existingLake == null)
                    {
                        existingLake = new LakeRegulation
                        {
                            LakeName = lakeName,
                            Confidence = table.Confidence
                        };
                        lakes.Add(existingLake);
                    }

                    // Extract species regulation
                    if (speciesIndex != -1 && row.Cells.Count > speciesIndex)
                    {
                        var speciesName = row.Cells[speciesIndex].Content?.Trim();
                        if (!string.IsNullOrWhiteSpace(speciesName))
                        {
                            var speciesReg = new SpeciesRegulation
                            {
                                SpeciesName = speciesName,
                                Confidence = row.Cells[speciesIndex].Confidence
                            };

                            if (seasonIndex != -1 && row.Cells.Count > seasonIndex)
                            {
                                speciesReg.Season = row.Cells[seasonIndex].Content?.Trim() ?? string.Empty;
                            }

                            if (limitIndex != -1 && row.Cells.Count > limitIndex)
                            {
                                var limitText = row.Cells[limitIndex].Content?.Trim() ?? string.Empty;
                                ExtractLimitsFromText(limitText, speciesReg);
                            }

                            existingLake.Species.Add(speciesReg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing table for lake regulations");
            }
        }

        return lakes;
    }

    private List<LakeRegulation> ExtractFromText(Dictionary<string, ExtractedField> fields)
    {
        var lakes = new List<LakeRegulation>();

        try
        {
            // Combine all text content
            var allText = string.Join(" ", fields.Values.Select(f => f.Value));

            // Use regex patterns to find lake names and regulations
            var lakePatterns = new[]
            {
                @"([A-Za-z\s]+Lake)",
                @"Lake\s+([A-Za-z\s]+)",
                @"([A-Za-z\s]+)\s+Lake"
            };

            foreach (var pattern in lakePatterns)
            {
                var matches = Regex.Matches(allText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var lakeName = match.Groups[1].Value.Trim();
                    if (lakeName.Length > 2 && !lakes.Any(l => 
                        string.Equals(l.LakeName, lakeName, StringComparison.OrdinalIgnoreCase)))
                    {
                        lakes.Add(new LakeRegulation
                        {
                            LakeName = lakeName,
                            Confidence = 0.7 // Lower confidence for text extraction
                        });
                    }
                }
            }

            return lakes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting lakes from text");
            return lakes;
        }
    }

    private int FindColumnIndex(TableRow headerRow, string[] keywords)
    {
        for (int i = 0; i < headerRow.Cells.Count; i++)
        {
            var cellContent = headerRow.Cells[i].Content?.ToLower() ?? string.Empty;
            if (keywords.Any(keyword => cellContent.Contains(keyword)))
            {
                return i;
            }
        }
        return -1;
    }

    private void ExtractLimitsFromText(string limitText, SpeciesRegulation speciesReg)
    {
        try
        {
            // Extract bag limit (numbers followed by "bag", "limit", etc.)
            var bagLimitPattern = @"(\d+)\s*(?:bag|limit|daily)";
            var bagMatch = Regex.Match(limitText, bagLimitPattern, RegexOptions.IgnoreCase);
            if (bagMatch.Success && int.TryParse(bagMatch.Groups[1].Value, out var bagLimit))
            {
                speciesReg.BagLimit = bagLimit;
            }

            // Extract size limits
            var sizeLimitPattern = @"(\d+(?:\.\d+)?)\s*(?:inch|in|cm|minimum|maximum|min|max)";
            var sizeMatch = Regex.Match(limitText, sizeLimitPattern, RegexOptions.IgnoreCase);
            if (sizeMatch.Success)
            {
                speciesReg.SizeLimit = sizeMatch.Value;
            }

            // Add the entire text as a restriction if it contains relevant keywords
            var restrictionKeywords = new[] { "no", "prohibited", "catch", "release", "only", "special" };
            if (restrictionKeywords.Any(keyword => limitText.ToLower().Contains(keyword)))
            {
                speciesReg.Restrictions.Add(limitText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting limits from text: {LimitText}", limitText);
        }
    }
}
