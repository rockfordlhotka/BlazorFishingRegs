using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Linq;
using FishingRegs.Data;
using FishingRegs.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple regex-based database population - no AI complexity, just reliable pattern matching
/// </summary>
class RegexDatabasePopulation
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunRegexDatabasePopulation(string[] args)
    {
        AnsiConsole.Write(
            new Panel(new Text("Regex Database Population", style: "bold"))
                .BorderColor(Color.Green)
                .Header("[green]SIMPLE & RELIABLE[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[green]Populating database using regex pattern matching - no AI required![/]");
        AnsiConsole.WriteLine();

        try
        {
            // Setup services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            var configuration = BuildConfiguration();
            services.AddSingleton<IConfiguration>(configuration);

            // Add Entity Framework with in-memory database for testing
            services.AddDbContext<FishingRegsDbContext>(options =>
            {
                options.UseInMemoryDatabase("RegexTestDatabase");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            var serviceProvider = services.BuildServiceProvider();
            var dbContext = serviceProvider.GetRequiredService<FishingRegsDbContext>();
            var logger = serviceProvider.GetRequiredService<ILogger<RegexDatabasePopulation>>();

            // Ensure database is created
            await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("Database created/ensured");

            // Load the fishing regulations file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]? Test file not found:[/] {testTextPath}");
                return;
            }

            var textContent = await File.ReadAllTextAsync(testTextPath);
            AnsiConsole.MarkupLine($"[green]? Loaded document:[/] {textContent.Length:N0} characters");

            // Extract water bodies using regex
            AnsiConsole.Write(new Rule("[blue]Step 1: Extract Water Bodies[/]"));
            
            var waterBodies = ExtractWaterBodiesWithRegex(textContent);
            AnsiConsole.MarkupLine($"[green]? Extracted {waterBodies.Count} water bodies[/]");

            // Debug: Show first few extracted water bodies
            if (waterBodies.Count > 0)
            {
                AnsiConsole.MarkupLine("[dim]First 5 extracted water bodies:[/]");
                foreach (var wb in waterBodies.Take(5))
                {
                    AnsiConsole.MarkupLine($"[dim]  • {wb.Name} ({wb.County}) - {wb.WaterType}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]? No water bodies extracted! Regex may not be working.[/>");
                return;
            }

            // Extract unique counties
            var counties = waterBodies
                .Select(wb => wb.County)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            
            AnsiConsole.MarkupLine($"[green]? Found {counties.Count} unique counties[/]");

            // Debug: Show first few counties
            if (counties.Count > 0)
            {
                AnsiConsole.MarkupLine("[dim]First 5 counties:[/]");
                foreach (var county in counties.Take(5))
                {
                    AnsiConsole.MarkupLine($"[dim]  • {county}[/]");
                }
            }

            // Populate database
            AnsiConsole.Write(new Rule("[blue]Step 2: Populate Database[/]"));

            // Check if tables exist and can be queried
            try
            {
                var existingStatesCount = await dbContext.States.CountAsync();
                var existingCountiesCount = await dbContext.Counties.CountAsync();
                var existingWaterBodiesCount = await dbContext.WaterBodies.CountAsync();
                
                AnsiConsole.MarkupLine($"[dim]Existing records - States: {existingStatesCount}, Counties: {existingCountiesCount}, Water Bodies: {existingWaterBodiesCount}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]? Error querying existing data:[/] {ex.Message}");
                return;
            }

            // Ensure Minnesota state exists
            var minnesotaState = await dbContext.States.FirstOrDefaultAsync(s => s.Name == "Minnesota");
            if (minnesotaState == null)
            {
                AnsiConsole.MarkupLine("[yellow]Creating Minnesota state...[/]");
                minnesotaState = new State
                {
                    Name = "Minnesota",
                    Code = "MN",
                    Country = "US",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                
                try
                {
                    dbContext.States.Add(minnesotaState);
                    var stateChanges = await dbContext.SaveChangesAsync();
                    logger.LogInformation($"Created Minnesota state record - {stateChanges} changes saved");
                    AnsiConsole.MarkupLine($"[green]? Created Minnesota state (ID: {minnesotaState.Id})[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]? Error creating Minnesota state:[/] {ex.Message}");
                    return;
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Minnesota state already exists (ID: {minnesotaState.Id})[/]");
            }

            // Create counties
            var createdCounties = 0;
            var countyMap = new Dictionary<string, County>();

            AnsiConsole.MarkupLine("[yellow]Creating counties...[/]");
            foreach (var countyName in counties)
            {
                try
                {
                    var existingCounty = await dbContext.Counties
                        .FirstOrDefaultAsync(c => c.Name == countyName && c.StateId == minnesotaState.Id);

                    if (existingCounty == null)
                    {
                        var newCounty = new County
                        {
                            Name = countyName,
                            StateId = minnesotaState.Id,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        dbContext.Counties.Add(newCounty);
                        var countyChanges = await dbContext.SaveChangesAsync();
                        
                        if (countyChanges > 0)
                        {
                            countyMap[countyName] = newCounty;
                            createdCounties++;
                            logger.LogDebug($"Created county: {countyName} (ID: {newCounty.Id})");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]? Failed to save county: {countyName}[/]");
                        }
                    }
                    else
                    {
                        countyMap[countyName] = existingCounty;
                        logger.LogDebug($"County already exists: {countyName} (ID: {existingCounty.Id})");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]? Error processing county {countyName}:[/] {ex.Message}");
                    logger.LogError(ex, $"Error processing county {countyName}");
                }
            }

            AnsiConsole.MarkupLine($"[green]? Created {createdCounties} new counties[/]");

            // Create water bodies
            var createdWaterBodies = 0;
            var batchSize = 10; // Smaller batch size for better error tracking

            AnsiConsole.MarkupLine("[yellow]Creating water bodies...[/]");
            for (int i = 0; i < waterBodies.Count; i += batchSize)
            {
                var batch = waterBodies.Skip(i).Take(batchSize);
                
                foreach (var waterBodyInfo in batch)
                {
                    try
                    {
                        if (!countyMap.ContainsKey(waterBodyInfo.County))
                        {
                            AnsiConsole.MarkupLine($"[red]? County not found in map: {waterBodyInfo.County}[/]");
                            continue;
                        }

                        var county = countyMap[waterBodyInfo.County];

                        var existingWaterBody = await dbContext.WaterBodies
                            .FirstOrDefaultAsync(wb => wb.Name == waterBodyInfo.Name && wb.CountyId == county.Id);

                        if (existingWaterBody == null)
                        {
                            var newWaterBody = new WaterBody
                            {
                                Name = waterBodyInfo.Name,
                                StateId = minnesotaState.Id,
                                CountyId = county.Id,
                                WaterType = waterBodyInfo.WaterType,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                            };

                            dbContext.WaterBodies.Add(newWaterBody);
                            createdWaterBodies++;
                            logger.LogDebug($"Added water body: {waterBodyInfo.Name} ({waterBodyInfo.County})");
                        }
                        else
                        {
                            logger.LogDebug($"Water body already exists: {waterBodyInfo.Name} ({waterBodyInfo.County})");
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]? Error processing water body {waterBodyInfo.Name}:[/] {ex.Message}");
                        logger.LogError(ex, $"Error processing water body {waterBodyInfo.Name}");
                    }
                }

                // Save batch
                try
                {
                    var changes = await dbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        AnsiConsole.MarkupLine($"[dim]Saved batch {i / batchSize + 1}: {changes} changes saved[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]? Error saving batch {i / batchSize + 1}:[/] {ex.Message}");
                    logger.LogError(ex, $"Error saving batch {i / batchSize + 1}");
                }
            }

            AnsiConsole.MarkupLine($"[green]? Created {createdWaterBodies} new water bodies[/]");

            // Verify final counts
            try
            {
                var finalStatesCount = await dbContext.States.CountAsync();
                var finalCountiesCount = await dbContext.Counties.CountAsync();
                var finalWaterBodiesCount = await dbContext.WaterBodies.CountAsync();
                
                AnsiConsole.MarkupLine($"[cyan]Final database counts - States: {finalStatesCount}, Counties: {finalCountiesCount}, Water Bodies: {finalWaterBodiesCount}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]? Error getting final counts:[/] {ex.Message}");
            }

            // Summary
            AnsiConsole.Write(new Rule("[blue]Summary[/]"));
            
            var summaryTable = new Table()
                .AddColumn("Item")
                .AddColumn("Count");

            summaryTable.AddRow("Counties Created", createdCounties.ToString());
            summaryTable.AddRow("Water Bodies Created", createdWaterBodies.ToString());
            summaryTable.AddRow("Total Water Bodies Found", waterBodies.Count.ToString());
            summaryTable.AddRow("Processing Method", "[green]Regex Pattern Matching[/]");

            AnsiConsole.Write(summaryTable);

            // Show sample data
            AnsiConsole.Write(new Rule("[blue]Sample Data from Database[/]"));
            
            try
            {
                var sampleWaterBodies = await dbContext.WaterBodies
                    .Include(wb => wb.County)
                    .Take(10)
                    .ToListAsync();

                if (sampleWaterBodies.Any())
                {
                    foreach (var sample in sampleWaterBodies)
                    {
                        AnsiConsole.MarkupLine($"• [blue]{sample.Name}[/] ({sample.WaterType}) - [yellow]{sample.County?.Name}[/] County");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]? No water bodies found in database![/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]? Error querying sample data:[/] {ex.Message}");
            }

            AnsiConsole.MarkupLine($"\n[green]?? Regex database population completed![/]");
            AnsiConsole.MarkupLine("[dim]This approach is fast, reliable, and doesn't require AI tokens.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    /// <summary>
    /// Extracts water bodies using regex patterns - same logic as the extraction test
    /// </summary>
    private static List<WaterBodyInfo> ExtractWaterBodiesWithRegex(string text)
    {
        var waterBodies = new List<WaterBodyInfo>();
        var seenCombinations = new HashSet<string>();

        // Primary pattern: "WATER BODY NAME (County)"
        var primaryPattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s\.]+)\)";
        var matches = Regex.Matches(text, primaryPattern, RegexOptions.Multiline);

        AnsiConsole.MarkupLine($"[dim]Regex found {matches.Count} potential matches[/]");

        var validMatches = 0;
        var invalidMatches = 0;

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.Trim();
            var county = match.Groups[2].Value.Trim();

            // Clean and validate
            var cleanName = CleanWaterBodyName(name);
            var cleanCounty = CleanCountyName(county);

            if (IsValidWaterBodyEntry(cleanName, cleanCounty))
            {
                var waterType = DetermineWaterType(cleanName);
                var key = $"{cleanName}|{cleanCounty}".ToLowerInvariant();

                if (!seenCombinations.Contains(key))
                {
                    waterBodies.Add(new WaterBodyInfo
                    {
                        Name = cleanName,
                        County = cleanCounty,
                        WaterType = waterType,
                        State = "Minnesota"
                    });
                    
                    seenCombinations.Add(key);
                    validMatches++;
                }
            }
            else
            {
                invalidMatches++;
                // Uncomment the line below to see what's being filtered out
                // AnsiConsole.MarkupLine($"[dim]Filtered out: '{name}' ({county})[/]");
            }
        }

        AnsiConsole.MarkupLine($"[dim]Valid matches: {validMatches}, Invalid matches: {invalidMatches}, Unique water bodies: {waterBodies.Count}[/]");

        return waterBodies.OrderBy(wb => wb.County).ThenBy(wb => wb.Name).ToList();
    }

    // Helper methods for regex extraction and validation
    private static bool IsValidWaterBodyEntry(string name, string county)
    {
        // Basic validation rules
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(county))
            return false;

        if (name.Length < 3 || name.Length > 100)
            return false;

        if (county.Length < 3 || county.Length > 30)
            return false;

        // Filter out obvious non-water-body entries
        var excludePatterns = new[]
        {
            "SPECIES", "SEASON", "LIMIT", "ZONE", "OPEN", "CLOSED", 
            "POSSESSION", "SIZE", "DAILY", "WALLEYE", "PIKE", "BASS",
            "MUSKIE", "TROUT", "YEAR", "DNR", "ANGLING", "WATERS"
        };

        foreach (var pattern in excludePatterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // County validation - should look like a real county name
        var invalidCountyPatterns = new[] { "ONLY", "STREAMS", "RIVERS", "LAKES" };
        foreach (var pattern in invalidCountyPatterns)
        {
            if (county.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string DetermineWaterType(string name)
    {
        var nameLower = name.ToLowerInvariant();

        if (nameLower.Contains("river"))
            return "river";
        if (nameLower.Contains("stream") || nameLower.Contains("creek") || nameLower.Contains("brook"))
            return "stream";
        if (nameLower.Contains("pond"))
            return "pond";
        if (nameLower.Contains("reservoir"))
            return "reservoir";
        if (nameLower.Contains("chain") || nameLower.Contains("flowage"))
            return "chain";

        // Default to lake
        return "lake";
    }

    private static string CleanWaterBodyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove extra whitespace and periods
        name = Regex.Replace(name, @"\s+", " ").Trim();
        name = name.TrimEnd('.');

        // Convert to proper case
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                // Handle special cases
                if (words[i].Equals("ST", StringComparison.OrdinalIgnoreCase) || 
                    words[i].Equals("ST.", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = "St.";
                }
                else
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
                }
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }

        return string.Join(" ", words);
    }

    private static string CleanCountyName(string county)
    {
        if (string.IsNullOrWhiteSpace(county))
            return string.Empty;

        county = county.Trim();

        // Remove "County" suffix if present
        if (county.EndsWith("County", StringComparison.OrdinalIgnoreCase))
        {
            county = county[..^6].Trim();
        }

        // Remove extra periods and spaces
        county = Regex.Replace(county, @"\s+", " ").Trim();
        county = county.TrimEnd('.');

        // Convert to proper case
        var words = county.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
                // Handle special cases like "St. Louis"
                if (words[i].Equals("ST", StringComparison.OrdinalIgnoreCase) || 
                    words[i].Equals("ST.", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = "St.";
                }
                else
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
                }
            }
            else
            {
                words[i] = words[i].ToUpper();
            }
        }

        return string.Join(" ", words);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        if (isDevelopment)
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }
}