using Spectre.Console;

namespace FishingRegs.TestConsole;

public class LittleRabbitLakeTest
{
    public static async Task RunTest()
    {
        AnsiConsole.MarkupLine("[bold blue]=== Little Rabbit Lake Parsing Test ===[/]");
        
        // Test the exact scenario from the conversation - Little Rabbit Lake parsing
        var testSection = @"
⁕NEW—LITTLE RABBIT LAKE (Cook) See Burntside Lake (listed under Cook County).

LITTLE RICE LAKE (Beltrami) Northern pike: possession limit 2, only one over 26 inches. Walleye: possession limit 2, only one over 20 inches.

LITTLE STAR LAKE (Cass) See Star Lake-Cass.

LITTLE VERMILION LAKE (St. Louis) See Vermilion Lake.

LITTLE WINNIBIGOSHISH LAKE (Beltrami, Itasca) See Winnibigoshish Lake.

LIZZIE LAKE (Crow Wing) See Whitefish Chain.

LOBSTER LAKE (Hubbard) Walleye: possession limit 2, only one over 20 inches.

LOON LAKE (Cook) See lakes listed under Cook County.

LOON LAKE (Itasca) Northern pike: possession limit 2, only one over 26 inches. Walleye: possession limit 2, only one over 20 inches.
";

        Console.WriteLine($"Test section ({testSection.Length} chars):");
        Console.WriteLine(testSection);
        Console.WriteLine();

        var lakeEntries = new List<(string LakeName, string County, string RegulationText)>();

        try
        {
            // Use the same logic as our improved service
            var normalizedText = System.Text.RegularExpressions.Regex.Replace(testSection, @"\s+", " ").Trim();
            
            var expectedPatterns = new[]
            {
                // Look for compound entries first (more specific patterns)
                @"([A-Z][^()]*?(?:and connected|including|and|near)[^()]*?)\s*\(([^)]+)\)",
                // Then look for simple lake entries
                @"([A-Z][A-Z\s\-,&\.''\d]+)\s*\(([^)]+)\)"
            };

            var foundLakes = new HashSet<int>(); // Track positions to avoid duplicates
            
            foreach (var pattern in expectedPatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(normalizedText, pattern);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    // Skip if we already processed a lake at this position
                    if (foundLakes.Contains(match.Index))
                        continue;
                        
                    foundLakes.Add(match.Index);
                    
                    var lakeName = match.Groups[1].Value.Trim();
                    var county = match.Groups[2].Value.Trim();
                    
                    // Clean up lake name - remove leading symbols
                    lakeName = System.Text.RegularExpressions.Regex.Replace(lakeName, @"^[⁕NEW—]*", "").Trim();
                    
                    // Skip section headers and very short names
                    if (lakeName.Contains("National Wildlife") || 
                        lakeName.Contains("Voyageurs") || 
                        lakeName.Length < 3)
                    {
                        continue;
                    }
                    
                    // Find regulation text after this entry
                    var startPos = match.Index + match.Length;
                    var endPos = normalizedText.Length;
                    
                    // Find the next lake entry to determine where this regulation ends
                    var nextLakePattern = @"[A-Z][A-Z\s\-,&\.''\d]*?\s*\([^)]+\)";
                    var nextMatch = System.Text.RegularExpressions.Regex.Match(normalizedText.Substring(startPos), nextLakePattern);
                    if (nextMatch.Success)
                    {
                        endPos = startPos + nextMatch.Index;
                    }
                    
                    var regulationText = "";
                    if (startPos < endPos)
                    {
                        regulationText = normalizedText.Substring(startPos, endPos - startPos).Trim();
                        // Remove any leading colons or spaces
                        regulationText = System.Text.RegularExpressions.Regex.Replace(regulationText, @"^[:,\s]+", "").Trim();
                    }
                    
                    // Only add if we have meaningful regulation text
                    if (!string.IsNullOrWhiteSpace(regulationText) && regulationText.Length >= 5)
                    {
                        lakeEntries.Add((lakeName, county, regulationText));
                        Console.WriteLine($"✅ Found: '{lakeName}' ({county}) - '{regulationText}'");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine($"Found {lakeEntries.Count} lake entries:");
        Console.WriteLine();

        foreach (var (lakeName, county, regulation) in lakeEntries)
        {
            Console.WriteLine($"Lake: {lakeName}");
            Console.WriteLine($"County: {county}");
            Console.WriteLine($"Regulation: {regulation}");
            Console.WriteLine("---");
        }
        
        // Check for the specific lakes we care about
        var littleRabbitFound = lakeEntries.Any(l => l.LakeName.Contains("LITTLE RABBIT"));
        var littleRiceFound = lakeEntries.Any(l => l.LakeName.Contains("LITTLE RICE"));
        var loonItascaFound = lakeEntries.Any(l => l.LakeName.Contains("LOON LAKE") && l.County.Contains("Itasca"));
        
        Console.WriteLine();
        if (littleRabbitFound)
        {
            Console.WriteLine("✅ SUCCESS: LITTLE RABBIT LAKE found!");
            var littleRabbit = lakeEntries.First(l => l.LakeName.Contains("LITTLE RABBIT"));
            Console.WriteLine($"Full name: {littleRabbit.LakeName}");
            Console.WriteLine($"County: {littleRabbit.County}");
            Console.WriteLine($"Regulation: {littleRabbit.RegulationText}");
        }
        else
        {
            Console.WriteLine("❌ FAILED: LITTLE RABBIT LAKE not found!");
        }
        
        if (littleRiceFound)
        {
            Console.WriteLine("✅ SUCCESS: LITTLE RICE LAKE found!");
        }
        else
        {
            Console.WriteLine("❌ FAILED: LITTLE RICE LAKE not found!");
        }
        
        if (loonItascaFound)
        {
            Console.WriteLine("✅ SUCCESS: LOON LAKE (Itasca) found!");
        }
        else
        {
            Console.WriteLine("❌ FAILED: LOON LAKE (Itasca) not found!");
        }
    }
}