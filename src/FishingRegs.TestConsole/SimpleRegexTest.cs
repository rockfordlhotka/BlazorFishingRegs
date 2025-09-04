using System;
using System.IO;
using System.Text.RegularExpressions;

var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
var textContent = await File.ReadAllTextAsync(testTextPath);
Console.WriteLine($"Text file length: {textContent.Length} characters\n");

// Find all instances
var sectionPattern = @"WATERS WITH EXPERIMENTAL AND\s*SPECIAL REGULATIONS";
var sectionMatches = Regex.Matches(textContent, sectionPattern, RegexOptions.IgnoreCase);

Console.WriteLine($"Found {sectionMatches.Count} section pattern matches");

for (int i = 0; i < sectionMatches.Count; i++)
{
    var match = sectionMatches[i];
    Console.WriteLine($"Match {i + 1} at index: {match.Index}");
    var context = textContent.Substring(match.Index, Math.Min(200, textContent.Length - match.Index));
    Console.WriteLine($"  Context: '{context.Replace('\n', ' ').Replace('\r', ' ').Substring(0, Math.Min(150, context.Length))}...'");
}

// Use the last match
if (sectionMatches.Count > 1)
{
    var lastMatch = sectionMatches[sectionMatches.Count - 1];
    Console.WriteLine($"\nUsing last match at index {lastMatch.Index}");
    
    // Extract section 
    var startIndex = lastMatch.Index;
    var sectionText = textContent.Substring(startIndex, Math.Min(2000, textContent.Length - startIndex));
    
    Console.WriteLine($"First 500 chars of last section:\n{sectionText.Substring(0, Math.Min(500, sectionText.Length))}");
    
    // Test lake parsing on this section
    var lakePattern = @"^([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)\s*(.*)";
    var lines = sectionText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var foundLakes = 0;
    
    Console.WriteLine("\nSearching for lake entries:");
    foreach (var line in lines.Take(20))
    {
        var trimmedLine = line.Trim();
        var lakeMatch = Regex.Match(trimmedLine, lakePattern);
        
        if (lakeMatch.Success && lakeMatch.Groups[1].Value.Length > 3)
        {
            foundLakes++;
            Console.WriteLine($"  Lake {foundLakes}: {lakeMatch.Groups[1].Value.Trim()} ({lakeMatch.Groups[2].Value.Trim()})");
            if (foundLakes >= 5) break;
        }
    }
    
    if (foundLakes == 0)
    {
        Console.WriteLine("No lakes found. Sample lines:");
        foreach (var line in lines.Take(10))
        {
            Console.WriteLine($"  '{line.Trim()}'");
        }
    }
}
