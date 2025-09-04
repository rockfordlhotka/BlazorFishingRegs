using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishingRegs.TextProcessingTest;

/// <summary>
/// Simple console application to test the text-based PDF processing strategy
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== FishingRegs Text-Based PDF Processing Test ===");
        Console.WriteLine();

        // Set up dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register only the text processing services (no Azure services needed for this test)
                services.AddScoped<IPdfTextExtractionService, PdfTextExtractionService>();
                services.AddScoped<ITextChunkingService, TextChunkingService>();
                
                // Register console application
                services.AddSingleton<TestApplication>();
            })
            .Build();

        try
        {
            // Run the test application
            var app = host.Services.GetRequiredService<TestApplication>();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running test application: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}

/// <summary>
/// Test application to demonstrate PDF text processing
/// </summary>
public class TestApplication
{
    private readonly IPdfTextExtractionService _textExtractionService;
    private readonly ITextChunkingService _textChunkingService;
    private readonly ILogger<TestApplication> _logger;

    public TestApplication(
        IPdfTextExtractionService textExtractionService,
        ITextChunkingService textChunkingService,
        ILogger<TestApplication> logger)
    {
        _textExtractionService = textExtractionService;
        _textChunkingService = textChunkingService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("1. Testing text extraction and chunking...");
        await TestTextExtractionAndChunking();

        Console.WriteLine("\nTest completed successfully!");
    }

    private async Task TestTextExtractionAndChunking()
    {
        try
        {
            // Test with the fishing regulations PDF
            var pdfPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.pdf";
            
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"PDF file not found: {pdfPath}");
                return;
            }

            Console.WriteLine($"Extracting text from: {pdfPath}");
            
            // Extract text
            using var pdfStream = File.OpenRead(pdfPath);
            var textResult = await _textExtractionService.ExtractTextAsync(pdfStream, Path.GetFileName(pdfPath));
            
            if (textResult.IsSuccess)
            {
                Console.WriteLine($"✓ Text extraction successful using {textResult.ExtractionMethod}");
                Console.WriteLine($"  Extracted {textResult.CharacterCount:N0} characters");
                Console.WriteLine($"  Estimated {textResult.EstimatedPageCount} pages");
                Console.WriteLine($"  Extraction time: {textResult.ExtractionTime.TotalSeconds:F2} seconds");
                
                // Show first 500 characters as preview
                var preview = textResult.ExtractedText.Length > 500 
                    ? textResult.ExtractedText.Substring(0, 500) + "..."
                    : textResult.ExtractedText;
                Console.WriteLine($"  Text preview: {preview}");
                
                // Test chunking
                Console.WriteLine("\nTesting text chunking...");
                var chunkingResult = _textChunkingService.ChunkText(textResult.ExtractedText, 4000, 200);
                
                if (chunkingResult.IsSuccess)
                {
                    Console.WriteLine($"✓ Text chunking successful");
                    Console.WriteLine($"  Created {chunkingResult.Chunks.Count} chunks");
                    Console.WriteLine($"  Total chunk characters: {chunkingResult.TotalChunkCharacters:N0}");
                    
                    // Show chunk details
                    for (int i = 0; i < Math.Min(3, chunkingResult.Chunks.Count); i++)
                    {
                        var chunk = chunkingResult.Chunks[i];
                        Console.WriteLine($"  Chunk {chunk.ChunkNumber}: {chunk.CharacterCount} chars, fishing content: {chunk.ContainsFishingContent}");
                    }
                    
                    if (chunkingResult.Chunks.Count > 3)
                    {
                        Console.WriteLine($"  ... and {chunkingResult.Chunks.Count - 3} more chunks");
                    }
                    
                    // Test filtering for fishing content
                    var filteredResult = _textChunkingService.FilterFishingChunks(chunkingResult);
                    Console.WriteLine($"  Fishing content chunks: {filteredResult.Chunks.Count} out of {chunkingResult.Chunks.Count}");
                    
                    // Validate chunking
                    var validation = _textChunkingService.ValidateChunking(textResult.ExtractedText, chunkingResult);
                    Console.WriteLine($"  Validation: {(validation.IsValid ? "✓ Valid" : "✗ Issues found")}");
                    Console.WriteLine($"  Coverage: {validation.CoveragePercentage:F1}%");
                    Console.WriteLine($"  Fishing content: {validation.FishingContentPercentage:F1}%");
                    Console.WriteLine($"  Quality score: {validation.QualityScore:F2}");
                    
                    if (validation.Issues.Any())
                    {
                        Console.WriteLine("  Issues:");
                        foreach (var issue in validation.Issues)
                        {
                            Console.WriteLine($"    - {issue}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Text chunking failed: {chunkingResult.ErrorMessage}");
                }
            }
            else
            {
                Console.WriteLine($"✗ Text extraction failed: {textResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error in text extraction test: {ex.Message}");
            _logger.LogError(ex, "Error in text extraction test");
        }
    }
}
