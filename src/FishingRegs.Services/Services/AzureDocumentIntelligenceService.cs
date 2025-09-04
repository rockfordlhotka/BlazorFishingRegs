using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using FishingRegs.Services.Extensions;

namespace FishingRegs.Services.Services;

/// <summary>
/// Azure Document Intelligence service implementation
/// </summary>
public class AzureDocumentIntelligenceService : IAzureDocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;

    public AzureDocumentIntelligenceService(
        IConfiguration configuration,
        ILogger<AzureDocumentIntelligenceService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var endpoint = configuration.GetSecureValue("AzureAI:DocumentIntelligence:Endpoint") 
            ?? throw new InvalidOperationException("Azure Document Intelligence endpoint not configured. Please add to User Secrets or Key Vault.");
        var apiKey = configuration.GetSecureValue("AzureAI:DocumentIntelligence:ApiKey") 
            ?? throw new InvalidOperationException("Azure Document Intelligence API key not configured. Please add to User Secrets or Key Vault.");

        _client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        string documentUrl,
        string modelId = "prebuilt-document",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document analysis for URL: {DocumentUrl} using model: {ModelId}", 
                documentUrl, modelId);

            var content = new AnalyzeDocumentContent()
            {
                UrlSource = new Uri(documentUrl)
            };

            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed, 
                modelId, 
                content, 
                cancellationToken: cancellationToken);

            var result = operation.Value;

            _logger.LogInformation("Document analysis completed successfully for URL: {DocumentUrl}", documentUrl);

            return MapToAnalysisResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze document from URL: {DocumentUrl}", documentUrl);
            return new DocumentAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Stream documentStream,
        string contentType,
        string modelId = "prebuilt-document",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document analysis from stream using model: {ModelId}", modelId);

            var content = new AnalyzeDocumentContent()
            {
                Base64Source = BinaryData.FromStream(documentStream)
            };

            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                modelId,
                content,
                cancellationToken: cancellationToken);

            var result = operation.Value;

            _logger.LogInformation("Document analysis completed successfully from stream");

            return MapToAnalysisResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze document from stream");
            return new DocumentAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    private DocumentAnalysisResult MapToAnalysisResult(AnalyzeResult result)
    {
        var analysisResult = new DocumentAnalysisResult
        {
            DocumentType = DetermineDocumentType(result),
            ExtractedFields = ExtractKeyValuePairs(result),
            Tables = ExtractTables(result),
            ConfidenceScores = CalculateConfidenceScores(result),
            ProcessedAt = DateTime.UtcNow,
            IsSuccess = true
        };

        return analysisResult;
    }

    private string DetermineDocumentType(AnalyzeResult result)
    {
        // Check for fishing regulation indicators in the content
        var allText = string.Join(" ", result.Pages?.SelectMany(p => p.Lines?.Select(l => l.Content) ?? Enumerable.Empty<string>()) ?? Enumerable.Empty<string>());
        
        if (allText.ToLower().Contains("fishing") && allText.ToLower().Contains("regulation"))
        {
            return "FishingRegulation";
        }

        return "Unknown";
    }

    private Dictionary<string, ExtractedField> ExtractKeyValuePairs(AnalyzeResult result)
    {
        var fields = new Dictionary<string, ExtractedField>();

        if (result.Documents != null)
        {
            foreach (var document in result.Documents)
            {
                if (document.Fields != null)
                {
                    foreach (var field in document.Fields)
                    {
                        fields[field.Key] = new ExtractedField
                        {
                            Name = field.Key,
                            Value = field.Value.Content ?? string.Empty,
                            Confidence = field.Value.Confidence ?? 0,
                            BoundingBox = ExtractBoundingBox(field.Value),
                            FieldType = MapFieldType(field.Value.Type)
                        };
                    }
                }
            }
        }

        // Also extract key-value pairs from pages if available
        // Note: KeyValuePairs may not be available in the current API version
        if (result.Pages != null)
        {
            foreach (var page in result.Pages)
            {
                // For now, we'll just extract from lines if key-value pairs aren't available
                if (page.Lines != null)
                {
                    var pageText = string.Join(" ", page.Lines.Select(l => l.Content));
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        fields[$"Page_{page.PageNumber}_Text"] = new ExtractedField
                        {
                            Name = $"Page_{page.PageNumber}_Text",
                            Value = pageText,
                            Confidence = 1.0,
                            FieldType = "Text"
                        };
                    }
                }
            }
        }

        return fields;
    }

    private List<ExtractedTable> ExtractTables(AnalyzeResult result)
    {
        var tables = new List<ExtractedTable>();

        if (result.Tables != null)
        {
            foreach (var table in result.Tables)
            {
                var extractedTable = new ExtractedTable
                {
                    RowCount = table.RowCount,
                    ColumnCount = table.ColumnCount,
                    Confidence = 1.0, // Tables don't have confidence in the current API
                    Rows = ExtractTableRows(table)
                };

                tables.Add(extractedTable);
            }
        }

        return tables;
    }

    private List<TableRow> ExtractTableRows(DocumentTable table)
    {
        var rows = new Dictionary<int, TableRow>();

        if (table.Cells != null)
        {
            foreach (var cell in table.Cells)
            {
                if (!rows.ContainsKey(cell.RowIndex))
                {
                    rows[cell.RowIndex] = new TableRow
                    {
                        RowIndex = cell.RowIndex,
                        Cells = new List<TableCell>()
                    };
                }

                rows[cell.RowIndex].Cells.Add(new TableCell
                {
                    RowIndex = cell.RowIndex,
                    ColumnIndex = cell.ColumnIndex,
                    Content = cell.Content ?? string.Empty,
                    Confidence = 1.0, // Cells don't have confidence in the current API
                    BoundingBox = ExtractBoundingBox(cell)
                });
            }
        }

        return rows.Values.OrderBy(r => r.RowIndex).ToList();
    }

    private Dictionary<string, double> CalculateConfidenceScores(AnalyzeResult result)
    {
        var scores = new Dictionary<string, double>();

        if (result.Documents != null)
        {
            var documentConfidences = result.Documents
                .SelectMany(d => d.Fields?.Values ?? Enumerable.Empty<DocumentField>())
                .Where(f => f.Confidence.HasValue)
                .Select(f => f.Confidence!.Value);

            if (documentConfidences.Any())
            {
                scores["OverallConfidence"] = documentConfidences.Average();
                scores["MinConfidence"] = documentConfidences.Min();
                scores["MaxConfidence"] = documentConfidences.Max();
            }
        }

        return scores;
    }

    private BoundingBox? ExtractBoundingBox(DocumentField field)
    {
        if (field.BoundingRegions?.Any() == true)
        {
            var region = field.BoundingRegions.First();
            if (region.Polygon?.Count >= 4)
            {
                var points = region.Polygon;
                // Handle the polygon points correctly - they should be System.Drawing.PointF or similar
                // For now, return a simple bounding box
                return new BoundingBox
                {
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100
                };
            }
        }

        return null;
    }

    private BoundingBox? ExtractBoundingBox(DocumentTableCell cell)
    {
        if (cell.BoundingRegions?.Any() == true)
        {
            var region = cell.BoundingRegions.First();
            if (region.Polygon?.Count >= 4)
            {
                var points = region.Polygon;
                // Handle the polygon points correctly - they should be System.Drawing.PointF or similar
                // For now, return a simple bounding box
                return new BoundingBox
                {
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100
                };
            }
        }

        return null;
    }

    private string MapFieldType(DocumentFieldType? fieldType)
    {
        return fieldType?.ToString() ?? "Unknown";
    }
}
