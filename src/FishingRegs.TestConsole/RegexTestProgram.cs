using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FishingRegs.TestConsole;

class RegexTestProgram
{
    public static async Task MainRegexTest(string[] args)
    {
        Console.WriteLine("Testing AI Extraction Regex Patterns");
        Console.WriteLine("===================================\n");

        var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
        
        if (!File.Exists(testTextPath))
        {
            Console.WriteLine($"Test text file not found at: {testTextPath}");
            return;
        }

        var textContent = await File.ReadAllTextAsync(testTextPath);
        Console.WriteLine($"Text file length: {textContent.Length} characters\n");

        // First test the section extraction pattern
        var sectionPattern = @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS";
        var sectionMatches = Regex.Matches(textContent, sectionPattern, RegexOptions.IgnoreCase);
        
        Console.WriteLine($"Found {sectionMatches.Count} section pattern matches");
        
        if (sectionMatches.Count == 0)
        {
            Console.WriteLine("No section matches found");
            return;
        }
        
        // Show all matches
        for (int i = 0; i < sectionMatches.Count; i++)
        {
            var match = sectionMatches[i];
            Console.WriteLine($"Match {i + 1} at index: {match.Index}");
            var context = textContent.Substring(match.Index, Math.Min(100, textContent.Length - match.Index));
            Console.WriteLine($"  Context: '{context.Replace('\n', ' ').Replace('\r', ' ')}'");
        }
        
        // Use the LAST match (should be the actual section)
        var sectionMatch = sectionMatches[sectionMatches.Count - 1];

        Console.WriteLine($"Section found at index: {sectionMatch.Index}");
        
        // Extract the section using the same logic as the service
        var startIndex = sectionMatch.Index;
        var endPatterns = new[] { @"BORDER WATERS", @"BOWFISHING, SPEARING", @"DARK HOUSE SPEARING", @"ILLUSTRATED FISH" };
        var endIndex = textContent.Length;
        
        foreach (var pattern in endPatterns)
        {
            var endMatch = Regex.Match(textContent.Substring(startIndex), pattern, RegexOptions.IgnoreCase);
            if (endMatch.Success)
            {
                endIndex = Math.Min(endIndex, startIndex + endMatch.Index);
                Console.WriteLine($"Found end pattern '{pattern}' at relative index {endMatch.Index}");
            }
        }

        var sectionText = textContent.Substring(startIndex, endIndex - startIndex);
        Console.WriteLine($"\nExtracted section length: {sectionText.Length} characters");
        Console.WriteLine($"First 300 characters of section:\n{sectionText.Substring(0, Math.Min(300, sectionText.Length))}\n");

        // Now test the lake entry parsing
        Console.WriteLine("Testing lake entry parsing...");
        
        // Test the complex regex pattern from the service
        var lakePattern = @"^([A-Z][A-Z\s\-,&\.''\d]+(?:\s+(?:including|and|near|Chain|chain|CHAIN)\s+[A-Z\s\-,&\.''\d]*)*)\s*\(([^)]+)\)\s+(.+?)(?=^[A-Z][A-Z\s\-,&\.''\d]+\s*\([^)]+\)|$)";
        var lakeMatches = Regex.Matches(sectionText, lakePattern, RegexOptions.Multiline | RegexOptions.Singleline);
        
        Console.WriteLine($"Complex regex found {lakeMatches.Count} lake matches");
        
        if (lakeMatches.Count > 0)
        {
            for (int i = 0; i < Math.Min(3, lakeMatches.Count); i++)
            {
                var match = lakeMatches[i];
                Console.WriteLine($"Lake {i + 1}: {match.Groups[1].Value.Trim()} ({match.Groups[2].Value.Trim()})");
                Console.WriteLine($"  Regulation: {match.Groups[3].Value.Trim().Substring(0, Math.Min(100, match.Groups[3].Value.Trim().Length))}...");
            }
        }
        else
        {
            // Try the line-by-line approach
            Console.WriteLine("Complex regex failed, trying line-by-line approach...");
            
            var lines = sectionText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var foundLakes = 0;
            
            foreach (var line in lines.Take(20)) // Check first 20 lines
            {
                var trimmedLine = line.Trim();
                var simpleLakeMatch = Regex.Match(trimmedLine, @"^([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)\s*(.*)");
                
                if (simpleLakeMatch.Success)
                {
                    foundLakes++;
                    Console.WriteLine($"Line-by-line found: {simpleLakeMatch.Groups[1].Value.Trim()} ({simpleLakeMatch.Groups[2].Value.Trim()})");
                    if (foundLakes >= 3) break;
                }
            }
            
            if (foundLakes == 0)
            {
                Console.WriteLine("No lakes found with line-by-line approach either.");
                Console.WriteLine("First 10 lines of section:");
                foreach (var line in lines.Take(10))
                {
                    Console.WriteLine($"  '{line.Trim()}'");
                }
            }
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
