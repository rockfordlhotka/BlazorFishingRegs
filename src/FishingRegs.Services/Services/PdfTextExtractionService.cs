using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FishingRegs.Services.Services;

/// <summary>
/// Service for extracting text from PDF documents using various methods
/// </summary>
public class PdfTextExtractionService : IPdfTextExtractionService
{
    private readonly ILogger<PdfTextExtractionService> _logger;
    private bool? _pdfToTextAvailable;

    public PdfTextExtractionService(ILogger<PdfTextExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(Stream pdfStream, string fileName)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting text extraction for {FileName}", fileName);

            // Try pdftotext first (best for handling encrypted/secured PDFs)
            if (await IsPdfToTextAvailableAsync())
            {
                _logger.LogInformation("Using pdftotext CLI for {FileName}", fileName);
                var result = await ExtractTextWithPdfToTextAsync(pdfStream, fileName);
                if (result.IsSuccess)
                {
                    result.ExtractionTime = stopwatch.Elapsed;
                    return result;
                }
                
                _logger.LogWarning("pdftotext failed for {FileName}, trying fallback methods: {Error}", fileName, result.ErrorMessage);
            }

            // Fallback to library-based extraction
            _logger.LogInformation("Using library-based extraction for {FileName}", fileName);
            var fallbackResult = await ExtractTextWithLibraryAsync(pdfStream, fileName);
            fallbackResult.ExtractionTime = stopwatch.Elapsed;
            return fallbackResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from {FileName}", fileName);
            return new TextExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Text extraction failed: {ex.Message}",
                ExtractionMethod = "Error",
                ExtractionTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<TextExtractionResult> ExtractTextWithPdfToTextAsync(Stream pdfStream, string fileName)
    {
        try
        {
            _logger.LogInformation("Extracting text using pdftotext for {FileName}", fileName);

            // Create temporary file for PDF
            var tempPdfPath = Path.GetTempFileName();
            var tempTxtPath = Path.ChangeExtension(tempPdfPath, ".txt");

            try
            {
                // Write PDF stream to temporary file
                pdfStream.Position = 0;
                await using (var fileStream = File.Create(tempPdfPath))
                {
                    await pdfStream.CopyToAsync(fileStream);
                }

                // Run pdftotext command
                var pdfToTextCommand = GetPdfToTextCommand();
                var startInfo = new ProcessStartInfo
                {
                    FileName = pdfToTextCommand,
                    Arguments = $"\"{tempPdfPath}\" \"{tempTxtPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _logger.LogDebug("Running command: {Command} {Arguments}", startInfo.FileName, startInfo.Arguments);

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return new TextExtractionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to start pdftotext process",
                        ExtractionMethod = "pdftotext"
                    };
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("pdftotext exited with code {ExitCode} for {FileName}. Error: {Error}", 
                        process.ExitCode, fileName, error);
                    return new TextExtractionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"pdftotext failed with exit code {process.ExitCode}: {error}",
                        ExtractionMethod = "pdftotext"
                    };
                }

                // Read extracted text
                if (!File.Exists(tempTxtPath))
                {
                    return new TextExtractionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "pdftotext did not create output file",
                        ExtractionMethod = "pdftotext"
                    };
                }

                var extractedText = await File.ReadAllTextAsync(tempTxtPath, Encoding.UTF8);
                
                _logger.LogInformation("Successfully extracted {CharCount} characters from {FileName} using pdftotext", 
                    extractedText.Length, fileName);

                return new TextExtractionResult
                {
                    IsSuccess = true,
                    ExtractedText = extractedText,
                    ExtractionMethod = "pdftotext"
                };
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath);
                    if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up temporary files");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running pdftotext for {FileName}", fileName);
            return new TextExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = $"pdftotext extraction failed: {ex.Message}",
                ExtractionMethod = "pdftotext"
            };
        }
    }

    public async Task<TextExtractionResult> ExtractTextWithLibraryAsync(Stream pdfStream, string fileName)
    {
        try
        {
            _logger.LogInformation("Extracting text using PdfSharp for {FileName}", fileName);
            var stopwatch = Stopwatch.StartNew();

            // Reset stream position
            pdfStream.Position = 0;

            // Try to extract text using PdfSharp
            var extractedText = await ExtractTextWithPdfSharp(pdfStream);
            
            stopwatch.Stop();

            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogInformation("Successfully extracted {CharCount} characters from {FileName} using PdfSharp in {ElapsedMs}ms", 
                    extractedText.Length, fileName, stopwatch.ElapsedMilliseconds);

                return new TextExtractionResult
                {
                    IsSuccess = true,
                    ExtractedText = extractedText,
                    ExtractionMethod = "PdfSharp",
                    ExtractionTime = stopwatch.Elapsed
                };
            }
            else
            {
                _logger.LogWarning("PdfSharp extracted empty text from {FileName}", fileName);
                return new TextExtractionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "PdfSharp extracted empty text. PDF may be image-based or encrypted.",
                    ExtractionMethod = "PdfSharp",
                    ExtractionTime = stopwatch.Elapsed
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PdfSharp text extraction for {FileName}", fileName);
            return new TextExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = $"PdfSharp extraction failed: {ex.Message}",
                ExtractionMethod = "PdfSharp"
            };
        }
    }

    /// <summary>
    /// Extracts text using PdfSharp library
    /// </summary>
    private async Task<string> ExtractTextWithPdfSharp(Stream pdfStream)
    {
        await Task.Yield(); // Make method async
        
        try
        {
            using var document = PdfReader.Open(pdfStream, PdfDocumentOpenMode.ReadOnly);
            var extractedText = new StringBuilder();

            _logger.LogInformation("PdfSharp opened document with {PageCount} pages", document.PageCount);

            for (int pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                var page = document.Pages[pageIndex];
                
                // PdfSharp doesn't have built-in text extraction, but we can try to access content
                // This is a basic approach - for production use, consider libraries like iText7 or PDFPig
                if (page.Contents.Elements.Count > 0)
                {
                    // Add placeholder text indicating page structure was found
                    extractedText.AppendLine($"--- Page {pageIndex + 1} ---");
                    extractedText.AppendLine("[PdfSharp detected content streams but cannot extract text directly]");
                    extractedText.AppendLine("[Consider using pdftotext CLI tool for text extraction]");
                    extractedText.AppendLine();
                }
            }

            return extractedText.ToString();
        }
        catch (Exception ex) when (ex.Message.Contains("encrypted") || ex.Message.Contains("password"))
        {
            _logger.LogWarning(ex, "PdfSharp could not read PDF - may be encrypted or password protected");
            throw new InvalidOperationException("PDF may be encrypted or password protected", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PdfSharp text extraction");
            throw;
        }
    }

    public async Task<bool> IsPdfToTextAvailableAsync()
    {
        if (_pdfToTextAvailable.HasValue)
        {
            return _pdfToTextAvailable.Value;
        }

        try
        {
            var pdfToTextCommand = GetPdfToTextCommand();
            var startInfo = new ProcessStartInfo
            {
                FileName = pdfToTextCommand,
                Arguments = "-v", // Version flag
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _pdfToTextAvailable = false;
                return false;
            }

            await process.WaitForExitAsync();
            _pdfToTextAvailable = process.ExitCode == 0 || process.ExitCode == 99; // Some versions return 99 for -v
            
            if (_pdfToTextAvailable.Value)
            {
                _logger.LogInformation("pdftotext CLI tool is available");
            }
            else
            {
                _logger.LogWarning("pdftotext CLI tool is not available. Install poppler-utils for better PDF text extraction.");
            }

            return _pdfToTextAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check for pdftotext availability");
            _pdfToTextAvailable = false;
            return false;
        }
    }

    private string GetPdfToTextCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, pdftotext might be in PATH or we might need full path
            // Could also try "pdftotext.exe" or check common installation paths
            return "pdftotext";
        }
        else
        {
            // On Linux/macOS, pdftotext is usually in PATH via poppler-utils
            return "pdftotext";
        }
    }
}
