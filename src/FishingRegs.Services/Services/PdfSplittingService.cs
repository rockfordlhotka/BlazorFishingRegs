using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for splitting large PDF documents into smaller chunks for processing
/// </summary>
public class PdfSplittingService : IPdfSplittingService
{
    private readonly ILogger<PdfSplittingService> _logger;
    private readonly IAzureDocumentIntelligenceService _documentService;
    private const int DEFAULT_MAX_SIZE_KB = 4000; // 4MB limit for Azure Document Intelligence
    private const int PAGES_PER_CHUNK = 10; // Start with 10 pages per chunk

    public PdfSplittingService(
        ILogger<PdfSplittingService> logger,
        IAzureDocumentIntelligenceService documentService)
    {
        _logger = logger;
        _documentService = documentService;
    }

    public async Task<PdfSplitResult> SplitPdfAsync(Stream pdfStream, string fileName, int maxSizeKb = DEFAULT_MAX_SIZE_KB)
    {
        try
        {
            _logger.LogInformation("Starting PDF split analysis for {FileName}", fileName);
            
            // Check if splitting is needed
            var originalSize = pdfStream.Length;
            var maxSizeBytes = maxSizeKb * 1024;
            
            if (originalSize <= maxSizeBytes)
            {
                _logger.LogInformation("PDF {FileName} size ({Size} bytes) is within limits, no splitting needed", fileName, originalSize);
                return new PdfSplitResult 
                { 
                    IsSuccess = true, 
                    RequiredSplitting = false,
                    Chunks = new List<PdfChunk>
                    {
                        new PdfChunk
                        {
                            ChunkNumber = 1,
                            Data = await ReadStreamToByteArrayAsync(pdfStream),
                            FileName = fileName,
                            PageStart = 1,
                            PageEnd = -1, // Will be determined when processing
                            SizeBytes = originalSize
                        }
                    }
                };
            }

            // Split the PDF
            _logger.LogInformation("PDF {FileName} size ({Size} bytes) exceeds limit ({MaxSize} bytes), splitting into chunks", 
                fileName, originalSize, maxSizeBytes);

            var chunks = await SplitPdfIntoChunksAsync(pdfStream, fileName, maxSizeKb);
            
            return new PdfSplitResult
            {
                IsSuccess = true,
                RequiredSplitting = true,
                Chunks = chunks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting PDF {FileName}", fileName);
            return new PdfSplitResult
            {
                IsSuccess = false,
                ErrorMessage = $"Error splitting PDF: {ex.Message}"
            };
        }
    }

    public async Task<DocumentAnalysisResult> ProcessSplitPdfAsync(Stream pdfStream, string fileName, string contentType)
    {
        try
        {
            _logger.LogInformation("Processing PDF with intelligent splitting: {FileName}", fileName);

            // First, try splitting the PDF
            var splitResult = await SplitPdfAsync(pdfStream, fileName);
            
            if (!splitResult.IsSuccess)
            {
                _logger.LogWarning("PDF splitting failed for {FileName}, attempting direct processing: {Error}", fileName, splitResult.ErrorMessage);
                
                // Fallback: try direct processing without splitting
                pdfStream.Position = 0;
                return await _documentService.AnalyzeDocumentAsync(pdfStream, contentType);
            }

            // Process each chunk
            var chunkResults = new List<DocumentAnalysisResult>();
            
            for (int i = 0; i < splitResult.Chunks.Count; i++)
            {
                var chunk = splitResult.Chunks[i];
                _logger.LogInformation("Processing chunk {ChunkNumber}/{TotalChunks} for {FileName}", 
                    i + 1, splitResult.Chunks.Count, fileName);

                using var chunkStream = new MemoryStream(chunk.Data);
                var chunkResult = await _documentService.AnalyzeDocumentAsync(chunkStream, contentType);
                
                if (chunkResult.IsSuccess)
                {
                    // Add chunk metadata
                    chunkResult.ChunkNumber = chunk.ChunkNumber;
                    chunkResult.PageStart = chunk.PageStart;
                    chunkResult.PageEnd = chunk.PageEnd;
                    chunkResults.Add(chunkResult);
                    
                    _logger.LogInformation("Successfully processed chunk {ChunkNumber} with {TableCount} tables and {FieldCount} fields", 
                        chunk.ChunkNumber, chunkResult.Tables.Count, chunkResult.ExtractedFields.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to process chunk {ChunkNumber} for {FileName}: {Error}", 
                        chunk.ChunkNumber, fileName, chunkResult.ErrorMessage);
                }

                // Add delay between API calls to avoid rate limiting
                if (i < splitResult.Chunks.Count - 1)
                {
                    await Task.Delay(1000); // 1 second delay between chunks
                }
            }

            // If no chunks were successfully processed, try direct processing as fallback
            if (!chunkResults.Any())
            {
                _logger.LogWarning("No chunks were successfully processed for {FileName}, attempting direct processing", fileName);
                pdfStream.Position = 0;
                return await _documentService.AnalyzeDocumentAsync(pdfStream, contentType);
            }

            // Merge results
            return await MergeAnalysisResults(chunkResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing split PDF {FileName}", fileName);
            
            // Final fallback: try direct processing
            try
            {
                _logger.LogInformation("Attempting direct processing as final fallback for {FileName}", fileName);
                pdfStream.Position = 0;
                return await _documentService.AnalyzeDocumentAsync(pdfStream, contentType);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Direct processing fallback also failed for {FileName}", fileName);
                return new DocumentAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error processing PDF (both splitting and direct methods failed): {ex.Message}; Fallback error: {fallbackEx.Message}"
                };
            }
        }
    }

    public Task<DocumentAnalysisResult> MergeAnalysisResults(IEnumerable<DocumentAnalysisResult> results)
    {
        try
        {
            var resultsList = results.Where(r => r.IsSuccess).ToList();
            
            if (!resultsList.Any())
            {
                return Task.FromResult(new DocumentAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No successful chunk results to merge"
                });
            }

            _logger.LogInformation("Merging {ChunkCount} chunk results", resultsList.Count);

            var mergedResult = new DocumentAnalysisResult
            {
                DocumentType = resultsList.First().DocumentType,
                ConfidenceScores = MergeConfidenceScores(resultsList),
                ProcessedAt = DateTime.UtcNow,
                ExtractedFields = new Dictionary<string, ExtractedField>(),
                Tables = new List<ExtractedTable>(),
                IsSuccess = true,
                ChunkNumber = 0, // Indicates merged result
                PageStart = resultsList.Min(r => r.PageStart),
                PageEnd = resultsList.Max(r => r.PageEnd)
            };

            // Merge extracted fields with page offset tracking
            int currentPageOffset = 0;
            foreach (var result in resultsList.OrderBy(r => r.ChunkNumber))
            {
                foreach (var field in result.ExtractedFields)
                {
                    var fieldKey = $"Chunk{result.ChunkNumber}_{field.Key}";
                    mergedResult.ExtractedFields[fieldKey] = new ExtractedField
                    {
                        Name = fieldKey,
                        Value = field.Value.Value,
                        Confidence = field.Value.Confidence,
                        BoundingBox = field.Value.BoundingBox,
                        FieldType = field.Value.FieldType
                    };
                }
                
                // Merge tables with page offset
                foreach (var table in result.Tables)
                {
                    var mergedTable = new ExtractedTable
                    {
                        RowCount = table.RowCount,
                        ColumnCount = table.ColumnCount,
                        Rows = table.Rows,
                        Confidence = table.Confidence
                    };
                    mergedResult.Tables.Add(mergedTable);
                }
                
                currentPageOffset += result.PageEnd - result.PageStart + 1;
            }

            _logger.LogInformation("Successfully merged results: {TableCount} tables, {FieldCount} fields", 
                mergedResult.Tables.Count, mergedResult.ExtractedFields.Count);

            return Task.FromResult(mergedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging analysis results");
            return Task.FromResult(new DocumentAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = $"Error merging results: {ex.Message}"
            });
        }
    }

    private Task<List<PdfChunk>> SplitPdfIntoChunksAsync(Stream pdfStream, string fileName, int maxSizeKb)
    {
        var chunks = new List<PdfChunk>();
        
        try
        {
            // Reset stream position
            pdfStream.Position = 0;
            
            // Try to load the PDF document
            PdfDocument document;
            try
            {
                document = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
            }
            catch (Exception pdfEx) when (pdfEx.Message.Contains("Crypt") || pdfEx.Message.Contains("Security") || pdfEx.Message.Contains("password"))
            {
                _logger.LogWarning("PDF {FileName} has security restrictions that prevent splitting: {Error}. Will process as single document.", fileName, pdfEx.Message);
                
                // Return the original document as a single chunk
                pdfStream.Position = 0;
                using var memoryStream = new MemoryStream();
                pdfStream.CopyTo(memoryStream);
                
                chunks.Add(new PdfChunk
                {
                    ChunkNumber = 1,
                    Data = memoryStream.ToArray(),
                    FileName = fileName,
                    PageStart = 1,
                    PageEnd = -1, // Unknown page count due to security restrictions
                    SizeBytes = pdfStream.Length
                });
                
                return Task.FromResult(chunks);
            }
            
            var totalPages = document.PageCount;
            
            _logger.LogInformation("PDF {FileName} has {PageCount} pages, splitting into chunks", fileName, totalPages);

            int currentPage = 1;
            int chunkNumber = 1;
            
            while (currentPage <= totalPages)
            {
                // Determine chunk size (start with PAGES_PER_CHUNK, adjust if needed)
                int pagesInChunk = Math.Min(PAGES_PER_CHUNK, totalPages - currentPage + 1);
                
                // Create chunk document
                var chunkDoc = new PdfDocument();
                
                for (int i = 0; i < pagesInChunk; i++)
                {
                    if (currentPage + i <= totalPages)
                    {
                        var page = document.Pages[currentPage + i - 1]; // 0-based index
                        chunkDoc.Pages.Add(page);
                    }
                }

                // Save chunk to memory stream
                using var chunkStream = new MemoryStream();
                chunkDoc.Save(chunkStream);
                chunkDoc.Dispose();
                
                var chunkData = chunkStream.ToArray();
                var chunkSizeKb = chunkData.Length / 1024.0;
                
                // If this chunk is still too large, reduce pages and try again
                if (chunkSizeKb > maxSizeKb && pagesInChunk > 1)
                {
                    _logger.LogInformation("Chunk {ChunkNumber} with {Pages} pages is {Size:F1}KB, reducing to fewer pages", 
                        chunkNumber, pagesInChunk, chunkSizeKb);
                    
                    pagesInChunk = Math.Max(1, pagesInChunk / 2);
                    continue; // Retry with fewer pages
                }

                chunks.Add(new PdfChunk
                {
                    ChunkNumber = chunkNumber,
                    Data = chunkData,
                    FileName = $"{Path.GetFileNameWithoutExtension(fileName)}_chunk_{chunkNumber:D2}.pdf",
                    PageStart = currentPage,
                    PageEnd = currentPage + pagesInChunk - 1,
                    SizeBytes = chunkData.Length
                });

                _logger.LogInformation("Created chunk {ChunkNumber}: pages {StartPage}-{EndPage}, size {Size:F1}KB", 
                    chunkNumber, currentPage, currentPage + pagesInChunk - 1, chunkSizeKb);

                currentPage += pagesInChunk;
                chunkNumber++;
            }

            document.Dispose();
            _logger.LogInformation("Successfully split PDF into {ChunkCount} chunks", chunks.Count);
            
            return Task.FromResult(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting PDF into chunks");
            throw;
        }
    }

    private Dictionary<string, double> MergeConfidenceScores(List<DocumentAnalysisResult> results)
    {
        var mergedScores = new Dictionary<string, double>();
        
        // Combine all confidence scores and calculate averages
        foreach (var result in results)
        {
            foreach (var score in result.ConfidenceScores)
            {
                var key = $"Chunk{result.ChunkNumber}_{score.Key}";
                mergedScores[key] = score.Value;
            }
        }
        
        // Add overall confidence as average of all chunk confidences
        if (mergedScores.Any())
        {
            mergedScores["Overall"] = mergedScores.Values.Average();
        }
        
        return mergedScores;
    }

    private static async Task<byte[]> ReadStreamToByteArrayAsync(Stream stream)
    {
        stream.Position = 0;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
