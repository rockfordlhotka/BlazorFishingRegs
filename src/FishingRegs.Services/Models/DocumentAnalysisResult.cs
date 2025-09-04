using System;
using System.Collections.Generic;

namespace FishingRegs.Services.Models;

/// <summary>
/// Result of Azure Document Intelligence analysis for fishing regulation PDFs
/// </summary>
public class DocumentAnalysisResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DocumentType { get; init; } = string.Empty;
    public Dictionary<string, ExtractedField> ExtractedFields { get; init; } = new();
    public List<ExtractedTable> Tables { get; init; } = new();
    public Dictionary<string, double> ConfidenceScores { get; init; } = new();
    public DateTime ProcessedAt { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents an extracted field from the document
/// </summary>
public class ExtractedField
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public BoundingBox? BoundingBox { get; init; }
    public string FieldType { get; init; } = string.Empty;
}

/// <summary>
/// Represents an extracted table from the document
/// </summary>
public class ExtractedTable
{
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public List<TableRow> Rows { get; init; } = new();
    public double Confidence { get; init; }
}

/// <summary>
/// Represents a row in an extracted table
/// </summary>
public class TableRow
{
    public int RowIndex { get; init; }
    public List<TableCell> Cells { get; init; } = new();
}

/// <summary>
/// Represents a cell in an extracted table
/// </summary>
public class TableCell
{
    public int RowIndex { get; init; }
    public int ColumnIndex { get; init; }
    public string Content { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public BoundingBox? BoundingBox { get; init; }
}

/// <summary>
/// Represents a bounding box for extracted content
/// </summary>
public class BoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}
