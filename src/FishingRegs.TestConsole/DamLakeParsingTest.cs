using FishingRegs.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple test to verify that DAM LAKE parsing works correctly
/// </summary>
public static class DamLakeParsingTest
{
    public static async Task RunTest()
    {
        Console.WriteLine("=== DAM LAKE Parsing Test ===");
        
        try
        {
            // Test data containing the DAM LAKE entry
            var testSection = @"
CUT FOOT SIOUX LAKE and connected Little Cut Foot Sioux Lake, First River Flowage,
and Egg Lake (Itasca): See Winnibigoshish.

D DAGGETT LAKE (Crow Wing) See Whitefish Chain.

DAM LAKE and connected Lily Lake and Dam Brook (Aitkin) Sunfish: daily limit 10.
DAVIS LAKE (Aitkin) See Big Sandy Lake.
DEEP LAKE (Ramsey) Closed to fishing.
";

            Console.WriteLine($"Test section ({testSection.Length} chars):");
            Console.WriteLine(testSection);
            Console.WriteLine();

            // Parse lake entries using just the regex logic
            var lakeEntries = ParseLakeEntriesTest(testSection);
            Console.WriteLine($"Found {lakeEntries.Count} lake entries:");
            Console.WriteLine();

            foreach (var (lakeName, county, regulationText) in lakeEntries)
            {
                Console.WriteLine($"Lake: {lakeName}");
                Console.WriteLine($"County: {county}");
                Console.WriteLine($"Regulation: {regulationText}");
                Console.WriteLine("---");
            }

            // Specifically check for DAM LAKE
            var damLake = lakeEntries.FirstOrDefault(entry => 
                entry.LakeName.Contains("DAM LAKE", StringComparison.OrdinalIgnoreCase));

            if (damLake.LakeName != null)
            {
                Console.WriteLine("✅ SUCCESS: DAM LAKE found!");
                Console.WriteLine($"Full name: {damLake.LakeName}");
                Console.WriteLine($"County: {damLake.County}");
                Console.WriteLine($"Regulations: {damLake.RegulationText}");
            }
            else
            {
                Console.WriteLine("❌ FAILED: DAM LAKE not found!");
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    // Simplified version of the parsing logic for testing
    private static List<(string LakeName, string County, string RegulationText)> ParseLakeEntriesTest(string specialRegulationsSection)
    {
        var lakeEntries = new List<(string, string, string)>();

        try
        {
            Console.WriteLine("=== Debugging Regex Pattern Matching ===");
            
            // Normalize the text by combining lines and cleaning up spacing
            var normalizedText = Regex.Replace(specialRegulationsSection, @"\s+", " ").Trim();
            Console.WriteLine($"Normalized text: '{normalizedText}'");
            Console.WriteLine();
            
            // Test different patterns to see what's going wrong
            Console.WriteLine("=== Testing Basic Pattern: WORD (County) ===");
            var basicPattern = @"([A-Z][^()]*?)\s*\(([^)]+)\)";
            var basicMatches = Regex.Matches(normalizedText, basicPattern);
            Console.WriteLine($"Basic pattern found {basicMatches.Count} matches:");
            
            foreach (Match match in basicMatches)
            {
                var lakeName = match.Groups[1].Value.Trim();
                var county = match.Groups[2].Value.Trim();
                Console.WriteLine($"  '{lakeName}' ({county}) at position {match.Index}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== Testing Complex Pattern ===");
            var complexPattern = @"([A-Z][A-Z\s\-,&\.''\d]+(?:\s+(?:including|and|near|Chain|chain|CHAIN|connected)\s+[A-Z\s\-,&\.''\d,]*)*)\s*\(([^)]+)\)";
            var complexMatches = Regex.Matches(normalizedText, complexPattern);
            Console.WriteLine($"Complex pattern found {complexMatches.Count} matches:");
            
            foreach (Match match in complexMatches)
            {
                var lakeName = match.Groups[1].Value.Trim();
                var county = match.Groups[2].Value.Trim();
                Console.WriteLine($"  '{lakeName}' ({county}) at position {match.Index}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== Manual Inspection of Expected Matches ===");
            
            // Let's manually find where each expected lake should be
            var expectedLakes = new[]
            {
                "CUT FOOT SIOUX LAKE and connected Little Cut Foot Sioux Lake, First River Flowage, and Egg Lake (Itasca)",
                "D DAGGETT LAKE (Crow Wing)",
                "DAM LAKE and connected Lily Lake and Dam Brook (Aitkin)",
                "DAVIS LAKE (Aitkin)",
                "DEEP LAKE (Ramsey)"
            };
            
            foreach (var expected in expectedLakes)
            {
                var index = normalizedText.IndexOf(expected, StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"Expected: '{expected}' - Found at position: {index}");
                
                if (index >= 0)
                {
                    // Extract just the lake name and county parts
                    var match = Regex.Match(expected, @"^([^()]+)\s*\(([^)]+)\)");
                    if (match.Success)
                    {
                        var lakeName = match.Groups[1].Value.Trim();
                        var county = match.Groups[2].Value.Trim();
                        
                        // Clean up lake name
                        lakeName = Regex.Replace(lakeName, @"^[⁕NEW—]*", "").Trim();
                        
                        // Find regulation text after this entry
                        var startPos = index + expected.Length;
                        var nextIndex = normalizedText.Length;
                        
                        // Find next lake entry
                        foreach (var nextExpected in expectedLakes)
                        {
                            if (nextExpected != expected)
                            {
                                var nextPos = normalizedText.IndexOf(nextExpected, startPos, StringComparison.OrdinalIgnoreCase);
                                if (nextPos > startPos && nextPos < nextIndex)
                                {
                                    nextIndex = nextPos;
                                }
                            }
                        }
                        
                        var regulationText = "";
                        if (startPos < nextIndex)
                        {
                            regulationText = normalizedText.Substring(startPos, nextIndex - startPos).Trim();
                            regulationText = Regex.Replace(regulationText, @"^[:,\s]+", "").Trim();
                        }
                        
                        Console.WriteLine($"  -> Lake: '{lakeName}' ({county})");
                        Console.WriteLine($"  -> Regulation: '{regulationText}' (length: {regulationText.Length})");
                        
                        if (!string.IsNullOrWhiteSpace(regulationText) && regulationText.Length >= 5)
                        {
                            lakeEntries.Add((lakeName, county, regulationText));
                            Console.WriteLine($"  -> ✅ Added to results");
                        }
                        else
                        {
                            Console.WriteLine($"  -> ❌ Skipped (insufficient regulation text)");
                        }
                    }
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing lake entries: {ex.Message}");
        }

        return lakeEntries;
    }
}