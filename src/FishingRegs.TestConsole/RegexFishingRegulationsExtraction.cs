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
/// Regex-based fishing regulations extraction and database population
/// </summary>
class RegexFishingRegulationsExtraction
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunRegexFishingRegulationsExtraction(string[] args)
    {
        AnsiConsole.Write(
            new Panel(new Text("Regex Fishing Regulations Extraction", style: "bold"))
                .BorderColor(Color.Blue)
                .Header("[blue]COMPREHENSIVE REGEX EXTRACTION[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[blue]Extracting fishing regulations using regex pattern matching[/]");
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
                options.UseInMemoryDatabase("RegexRegulationsDatabase");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            var serviceProvider = services.BuildServiceProvider();
            var dbContext = serviceProvider.GetRequiredService<FishingRegsDbContext>();
            var logger = serviceProvider.GetRequiredService<ILogger<RegexFishingRegulationsExtraction>>();

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

            // Step 1: Populate water bodies and counties first
            AnsiConsole.Write(new Rule("[blue]Step 1: Populate Water Bodies & Counties[/]"));
            await PopulateWaterBodiesAndCounties(dbContext, textContent, logger);

            // Step 2: Extract and populate fishing regulations
            AnsiConsole.Write(new Rule("[blue]Step 2: Extract Fishing Regulations[/]"));
            await ExtractAndPopulateFishingRegulations(dbContext, textContent, logger);

            // Step 3: Display summary and sample data
            AnsiConsole.Write(new Rule("[blue]Step 3: Summary & Sample Data[/]"));
            await DisplaySummaryAndSamples(dbContext);

            AnsiConsole.MarkupLine($"\n[green]?? Regex fishing regulations extraction completed![/]");
            AnsiConsole.MarkupLine("[dim]This approach extracts comprehensive fishing data using reliable regex patterns.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    private static async Task PopulateWaterBodiesAndCounties(FishingRegsDbContext dbContext, string textContent, ILogger logger)
    {
        var waterBodies = ExtractWaterBodiesWithRegex(textContent);
        AnsiConsole.MarkupLine($"[green]? Extracted {waterBodies.Count} water bodies[/]");

        var minnesotaState = await dbContext.States.FirstOrDefaultAsync(s => s.Name == "Minnesota");
        if (minnesotaState == null)
        {
            minnesotaState = new State
            {
                Name = "Minnesota",
                Code = "MN",
                Country = "US",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.States.Add(minnesotaState);
            await dbContext.SaveChangesAsync();
            AnsiConsole.MarkupLine($"[green]? Created Minnesota state[/]");
        }

        var counties = waterBodies.Select(wb => wb.County).Distinct().OrderBy(c => c).ToList();
        var countyMap = new Dictionary<string, County>();
        
        foreach (var countyName in counties)
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
                await dbContext.SaveChangesAsync();
                countyMap[countyName] = newCounty;
            }
            else
            {
                countyMap[countyName] = existingCounty;
            }
        }

        AnsiConsole.MarkupLine($"[green]? Processed {counties.Count} counties[/]");

        var createdWaterBodies = 0;
        foreach (var waterBodyInfo in waterBodies)
        {
            if (!countyMap.ContainsKey(waterBodyInfo.County))
                continue;

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
            }
        }

        await dbContext.SaveChangesAsync();
        AnsiConsole.MarkupLine($"[green]? Created {createdWaterBodies} water bodies[/]");
    }

    private static async Task ExtractAndPopulateFishingRegulations(FishingRegsDbContext dbContext, string textContent, ILogger logger)
    {
        var regulations = ExtractFishingRegulations(textContent);
        AnsiConsole.MarkupLine($"[green]? Extracted {regulations.Count} regulation entries[/]");

        if (regulations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]?? No fishing regulations found![/]");
            return;
        }

        var waterBodies = await dbContext.WaterBodies.Include(wb => wb.County).Include(wb => wb.State).ToListAsync();
        var fishSpecies = await dbContext.FishSpecies.ToListAsync();

        AnsiConsole.MarkupLine($"[dim]Database contains {waterBodies.Count} water bodies and {fishSpecies.Count} fish species[/]");

        var createdRegulations = 0;
        var currentYear = DateTime.Now.Year;

        foreach (var regInfo in regulations.Take(100))
        {
            try
            {
                var matchedWaterBody = FindMatchingWaterBody(regInfo.WaterBodyName, regInfo.County, waterBodies);
                if (matchedWaterBody == null) continue;

                var matchedSpecies = FindMatchingSpecies(regInfo.SpeciesName, fishSpecies);
                if (matchedSpecies == null) continue;

                var existingRegulation = await dbContext.FishingRegulations
                    .FirstOrDefaultAsync(fr => 
                        fr.WaterBodyId == matchedWaterBody.Id && 
                        fr.SpeciesId == matchedSpecies.Id && 
                        fr.RegulationYear == currentYear);

                if (existingRegulation == null)
                {
                    var newRegulation = new FishingRegulation
                    {
                        WaterBodyId = matchedWaterBody.Id,
                        SpeciesId = matchedSpecies.Id,
                        RegulationYear = currentYear,
                        RegulationType = "general",
                        EffectiveDate = new DateOnly(currentYear, 1, 1),
                        ExpirationDate = new DateOnly(currentYear, 12, 31),
                        DailyLimit = regInfo.DailyLimit,
                        PossessionLimit = regInfo.PossessionLimit,
                        Notes = regInfo.Notes,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    dbContext.FishingRegulations.Add(newRegulation);
                    createdRegulations++;

                    if (createdRegulations % 10 == 0)
                    {
                        await dbContext.SaveChangesAsync();
                        AnsiConsole.MarkupLine($"[dim]Saved {createdRegulations} regulations...[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error processing regulation for {regInfo.WaterBodyName} - {regInfo.SpeciesName}");
            }
        }

        await dbContext.SaveChangesAsync();
        AnsiConsole.MarkupLine($"[green]? Created {createdRegulations} fishing regulations[/]");
    }

    private static async Task DisplaySummaryAndSamples(FishingRegsDbContext dbContext)
    {
        try
        {
            var totalStates = await dbContext.States.CountAsync();
            var totalCounties = await dbContext.Counties.CountAsync();
            var totalWaterBodies = await dbContext.WaterBodies.CountAsync();
            var totalSpecies = await dbContext.FishSpecies.CountAsync();
            var totalRegulations = await dbContext.FishingRegulations.CountAsync();

            // Get additional statistics
            var waterBodiesWithRegulations = await dbContext.WaterBodies
                .Where(wb => wb.FishingRegulations.Any())
                .CountAsync();

            var speciesWithRegulations = await dbContext.FishSpecies
                .Where(fs => fs.FishingRegulations.Any())
                .CountAsync();

            var regulationsWithDailyLimits = await dbContext.FishingRegulations
                .Where(fr => fr.DailyLimit.HasValue)
                .CountAsync();

            var regulationsWithPossessionLimits = await dbContext.FishingRegulations
                .Where(fr => fr.PossessionLimit.HasValue)
                .CountAsync();

            var regulationsWithNotes = await dbContext.FishingRegulations
                .Where(fr => !string.IsNullOrEmpty(fr.Notes))
                .CountAsync();

            // Create comprehensive summary table
            var summaryTable = new Table()
                .Title("[bold blue]Extraction Summary[/]")
                .AddColumn(new TableColumn("Category").Centered())
                .AddColumn(new TableColumn("Count").Centered())
                .AddColumn(new TableColumn("Percentage").Centered());

            summaryTable.AddRow("[cyan]States[/]", totalStates.ToString(), "100%");
            summaryTable.AddRow("[cyan]Counties[/]", totalCounties.ToString(), "100%");
            summaryTable.AddRow("[cyan]Water Bodies[/]", totalWaterBodies.ToString(), "100%");
            summaryTable.AddRow("[cyan]Fish Species[/]", totalSpecies.ToString(), "100%");
            summaryTable.AddRow("[green]Fishing Regulations[/]", totalRegulations.ToString(), "100%");
            
            summaryTable.AddEmptyRow();
            summaryTable.AddRow("[yellow]Water Bodies with Regulations[/]", 
                waterBodiesWithRegulations.ToString(), 
                totalWaterBodies > 0 ? $"{(double)waterBodiesWithRegulations / totalWaterBodies * 100:F1}%" : "0%");
            
            summaryTable.AddRow("[yellow]Species with Regulations[/]", 
                speciesWithRegulations.ToString(), 
                totalSpecies > 0 ? $"{(double)speciesWithRegulations / totalSpecies * 100:F1}%" : "0%");

            summaryTable.AddEmptyRow();
            summaryTable.AddRow("[magenta]Regulations with Daily Limits[/]", 
                regulationsWithDailyLimits.ToString(), 
                totalRegulations > 0 ? $"{(double)regulationsWithDailyLimits / totalRegulations * 100:F1}%" : "0%");
            
            summaryTable.AddRow("[magenta]Regulations with Possession Limits[/]", 
                regulationsWithPossessionLimits.ToString(), 
                totalRegulations > 0 ? $"{(double)regulationsWithPossessionLimits / totalRegulations * 100:F1}%" : "0%");
            
            summaryTable.AddRow("[magenta]Regulations with Notes[/]", 
                regulationsWithNotes.ToString(), 
                totalRegulations > 0 ? $"{(double)regulationsWithNotes / totalRegulations * 100:F1}%" : "0%");

            AnsiConsole.Write(summaryTable);

            // Show top counties by water body count
            AnsiConsole.WriteLine();
            var topCounties = await dbContext.Counties
                .Include(c => c.WaterBodies)
                .OrderByDescending(c => c.WaterBodies.Count)
                .Take(5)
                .Select(c => new { c.Name, WaterBodyCount = c.WaterBodies.Count })
                .ToListAsync();

            if (topCounties.Any())
            {
                var countiesTable = new Table()
                    .Title("[bold cyan]Top Counties by Water Body Count[/]")
                    .AddColumn("County")
                    .AddColumn("Water Bodies");

                foreach (var county in topCounties)
                {
                    countiesTable.AddRow(county.Name, county.WaterBodyCount.ToString());
                }

                AnsiConsole.Write(countiesTable);
            }

            // Show species regulation breakdown
            AnsiConsole.WriteLine();
            var speciesBreakdown = await dbContext.FishSpecies
                .Include(fs => fs.FishingRegulations)
                .Where(fs => fs.FishingRegulations.Any())
                .OrderByDescending(fs => fs.FishingRegulations.Count)
                .Take(5)
                .Select(fs => new { 
                    fs.CommonName, 
                    RegulationCount = fs.FishingRegulations.Count,
                    AvgDailyLimit = fs.FishingRegulations.Where(fr => fr.DailyLimit.HasValue).Average(fr => (double?)fr.DailyLimit)
                })
                .ToListAsync();

            if (speciesBreakdown.Any())
            {
                var speciesTable = new Table()
                    .Title("[bold green]Species Regulation Breakdown[/]")
                    .AddColumn("Species")
                    .AddColumn("Regulations")
                    .AddColumn("Avg Daily Limit");

                foreach (var species in speciesBreakdown)
                {
                    var avgLimit = species.AvgDailyLimit.HasValue ? species.AvgDailyLimit.Value.ToString("F1") : "N/A";
                    speciesTable.AddRow(species.CommonName, species.RegulationCount.ToString(), avgLimit);
                }

                AnsiConsole.Write(speciesTable);
            }

            // Show sample regulations
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]Sample Fishing Regulations:[/]");

            var sampleRegulations = await dbContext.FishingRegulations
                .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.County)
                .Include(fr => fr.Species)
                .Take(10)
                .ToListAsync();

            foreach (var reg in sampleRegulations)
            {
                AnsiConsole.MarkupLine($"• [blue]{reg.WaterBody.Name}[/] ([yellow]{reg.WaterBody.County?.Name}[/]) - [green]{reg.Species.CommonName}[/]");
                
                if (reg.DailyLimit.HasValue)
                    AnsiConsole.MarkupLine($"  Daily Limit: [bold]{reg.DailyLimit}[/]");
                
                if (reg.PossessionLimit.HasValue)
                    AnsiConsole.MarkupLine($"  Possession Limit: [bold]{reg.PossessionLimit}[/]");
                
                if (!string.IsNullOrEmpty(reg.Notes))
                    AnsiConsole.MarkupLine($"  Notes: [dim]{reg.Notes.Substring(0, Math.Min(80, reg.Notes.Length))}...[/]");
                
                AnsiConsole.WriteLine();
            }

            // Show extraction effectiveness summary
            AnsiConsole.WriteLine();
            var effectivenessTable = new Table()
                .Title("[bold red]Extraction Effectiveness[/]")
                .AddColumn("Metric")
                .AddColumn("Value")
                .AddColumn("Status");

            var coverage = totalWaterBodies > 0 ? (double)waterBodiesWithRegulations / totalWaterBodies * 100 : 0;
            var coverageStatus = coverage switch
            {
                >= 80 => "[green]Excellent[/]",
                >= 60 => "[yellow]Good[/]",
                >= 40 => "[orange3]Fair[/]",
                _ => "[red]Needs Improvement[/]"
            };

            effectivenessTable.AddRow("Water Body Coverage", $"{coverage:F1}%", coverageStatus);
            effectivenessTable.AddRow("Regulations per Water Body", 
                waterBodiesWithRegulations > 0 ? $"{(double)totalRegulations / waterBodiesWithRegulations:F1}" : "0", 
                totalRegulations > waterBodiesWithRegulations ? "[green]Good[/]" : "[yellow]Limited[/]");
            effectivenessTable.AddRow("Species Coverage", 
                totalSpecies > 0 ? $"{(double)speciesWithRegulations / totalSpecies * 100:F1}%" : "0%",
                speciesWithRegulations >= 5 ? "[green]Good[/]" : "[yellow]Limited[/]");

            AnsiConsole.Write(effectivenessTable);

        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error displaying summary:[/] {ex.Message}");
        }
    }

    #region Public Static Methods for Reuse

    public static List<FishingRegulationInfo> ExtractFishingRegulations(string text)
    {
        var regulations = new List<FishingRegulationInfo>();
        
        // Look for bullet point regulations: "• Water Body (County): regulation text"
        // Updated pattern to capture multi-line regulation text better
        var bulletPattern = @"•\s*([A-Z][A-Za-z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s\.]+)\):\s*([^•]+?)(?=•|\n\s*[A-Z][A-Z]|\z)";
        var bulletMatches = Regex.Matches(text, bulletPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        AnsiConsole.WriteLine($"Found {bulletMatches.Count} bullet-point regulation entries");
        
        foreach (Match match in bulletMatches)
        {
            try
            {
                var waterBodyName = CleanWaterBodyName(match.Groups[1].Value.Trim());
                var county = CleanCountyName(match.Groups[2].Value.Trim());
                var regulationText = match.Groups[3].Value.Trim();

                if (!IsValidWaterBodyEntry(waterBodyName, county) || string.IsNullOrWhiteSpace(regulationText))
                    continue;

                var waterBodyRegulations = ParseRegulationText(regulationText, waterBodyName, county);
                regulations.AddRange(waterBodyRegulations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing regulation: {ex.Message}");
            }
        }

        // Also look for general statewide regulations that mention specific species and limits
        var statewideRegulations = ExtractStatewideRegulations(text);
        regulations.AddRange(statewideRegulations);

        // NEW: Look for detailed regulation entries in format: "WATER BODY (County) Species: details"
        var detailedRegulations = ExtractDetailedRegulations(text);
        regulations.AddRange(detailedRegulations);

        AnsiConsole.WriteLine($"Total regulations extracted: {regulations.Count}");

        return regulations.DistinctBy(r => $"{r.WaterBodyName}|{r.County}|{r.SpeciesName}").ToList();
    }

    private static List<FishingRegulationInfo> ExtractStatewideRegulations(string text)
    {
        var regulations = new List<FishingRegulationInfo>();
        
        // Look for statewide possession limits: "• Species possession limit is X"
        var statewidePattern = @"•\s*([A-Za-z\s]+?)\s*possession\s*limit\s*is\s*(\d+)";
        var matches = Regex.Matches(text, statewidePattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var speciesText = match.Groups[1].Value.Trim();
            if (int.TryParse(match.Groups[2].Value, out var limit))
            {
                var speciesName = MapSpeciesName(speciesText);
                if (!string.IsNullOrEmpty(speciesName))
                {
                    regulations.Add(new FishingRegulationInfo
                    {
                        WaterBodyName = "Statewide",
                        County = "All Counties",
                        SpeciesName = speciesName,
                        PossessionLimit = limit,
                        Notes = $"Statewide possession limit: {limit}"
                    });
                }
            }
        }

        return regulations;
    }

    private static List<FishingRegulationInfo> ExtractDetailedRegulations(string text)
    {
        var regulations = new List<FishingRegulationInfo>();
        
        // Enhanced pattern for: "WATER BODY (County) Species: regulation details"
        // This pattern is more flexible and handles various formatting
        var detailedPattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s]+)\)\s+([A-Za-z\s]+?):\s*([^\n]+)";
        var matches = Regex.Matches(text, detailedPattern, RegexOptions.Multiline);
        
        foreach (Match match in matches)
        {
            try
            {
                var waterBodyName = CleanWaterBodyName(match.Groups[1].Value.Trim());
                var county = CleanCountyName(match.Groups[2].Value.Trim());
                var speciesText = match.Groups[3].Value.Trim();
                var regulationText = match.Groups[4].Value.Trim();

                if (!IsValidWaterBodyEntry(waterBodyName, county))
                    continue;

                var speciesName = MapSpeciesName(speciesText);
                if (!string.IsNullOrEmpty(speciesName))
                {
                    var regulation = new FishingRegulationInfo
                    {
                        WaterBodyName = waterBodyName,
                        County = county,
                        SpeciesName = speciesName,
                        Notes = regulationText
                    };

                    // Extract daily limit from detailed text
                    var dailyLimitMatch = Regex.Match(regulationText, @"daily\s*limit\s*(\d+)", RegexOptions.IgnoreCase);
                    if (dailyLimitMatch.Success && int.TryParse(dailyLimitMatch.Groups[1].Value, out var dailyLimit))
                    {
                        regulation.DailyLimit = dailyLimit;
                    }

                    // Extract possession limit
                    var possessionMatch = Regex.Match(regulationText, @"possession\s*(?:limit\s*)?(\d+)", RegexOptions.IgnoreCase);
                    if (possessionMatch.Success && int.TryParse(possessionMatch.Groups[1].Value, out var possessionLimit))
                    {
                        regulation.PossessionLimit = possessionLimit;
                    }

                    // Extract size restrictions
                    var sizePattern = @"(\d+(?:\.\d+)?)\s*(?:to|-)?\s*(\d+(?:\.\d+)?)\s*(?:inch|in|"")";
                    var sizeMatch = Regex.Match(regulationText, sizePattern, RegexOptions.IgnoreCase);
                    if (sizeMatch.Success)
                    {
                        if (decimal.TryParse(sizeMatch.Groups[1].Value, out var size1) &&
                            decimal.TryParse(sizeMatch.Groups[2].Value, out var size2))
                        {
                            if (size1 < size2 && size1 > 5 && size2 < 50)
                            {
                                regulation.ProtectedSlotMinInches = size1;
                                regulation.ProtectedSlotMaxInches = size2;
                            }
                        }
                    }

                    regulations.Add(regulation);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing detailed regulation: {ex.Message}");
            }
        }

        // NEW: Also look for multi-species entries in a single line
        var multiSpeciesPattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s]+)\)\s+([^\n]+)";
        var multiMatches = Regex.Matches(text, multiSpeciesPattern, RegexOptions.Multiline);
        
        foreach (Match match in multiMatches)
        {
            try
            {
                var waterBodyName = CleanWaterBodyName(match.Groups[1].Value.Trim());
                var county = CleanCountyName(match.Groups[2].Value.Trim());
                var fullText = match.Groups[3].Value.Trim();

                if (!IsValidWaterBodyEntry(waterBodyName, county))
                    continue;

                // Look for multiple species mentioned in the same line
                var speciesSegments = SplitSpeciesSegments(fullText);
                foreach (var segment in speciesSegments)
                {
                    var speciesInSegment = ExtractSpeciesFromContext(segment);
                    if (!string.IsNullOrEmpty(speciesInSegment))
                    {
                        var regulation = new FishingRegulationInfo
                        {
                            WaterBodyName = waterBodyName,
                            County = county,
                            SpeciesName = speciesInSegment,
                            Notes = segment
                        };

                        // Extract limits specific to this segment
                        var dailyLimitMatch = Regex.Match(segment, @"daily\s*limit\s*(\d+)", RegexOptions.IgnoreCase);
                        if (dailyLimitMatch.Success && int.TryParse(dailyLimitMatch.Groups[1].Value, out var dailyLimit))
                        {
                            regulation.DailyLimit = dailyLimit;
                        }

                        regulations.Add(regulation);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing multi-species regulation: {ex.Message}");
            }
        }
        
        return regulations;
    }

    private static List<string> SplitSpeciesSegments(string text)
    {
        // Split text by common delimiters that separate species regulations
        var segments = new List<string>();
        
        // Split by periods that are followed by a capital letter (indicating new sentence/regulation)
        var periodSplits = Regex.Split(text, @"\.\s*(?=[A-Z])", RegexOptions.Multiline);
        
        foreach (var segment in periodSplits)
        {
            if (!string.IsNullOrWhiteSpace(segment))
            {
                segments.Add(segment.Trim());
            }
        }

        // If no period splits found, return the original text
        if (segments.Count == 0)
        {
            segments.Add(text);
        }

        return segments;
    }

    public static string MapSpeciesName(string speciesText)
    {
        var normalized = speciesText.ToLowerInvariant().Trim();
        
        // Remove common words that aren't species names
        normalized = Regex.Replace(normalized, @"\b(the|a|an|for|to|of|and|with|in|on|at|by|from|all|only)\b", "", RegexOptions.IgnoreCase).Trim();
        
        return normalized switch
        {
            var s when s.Contains("walleye") => "Walleye",
            var s when s.Contains("northern pike") || (s.Contains("pike") && !s.Contains("bass")) => "Northern Pike",
            var s when s.Contains("largemouth bass") || s.Contains("largemouth") => "Largemouth Bass",
            var s when s.Contains("smallmouth bass") || s.Contains("smallmouth") => "Smallmouth Bass",
            var s when s.Contains("bass") => "Largemouth Bass",
            var s when s.Contains("lake trout") => "Lake Trout",
            var s when s.Contains("brook trout") => "Brook Trout",
            var s when s.Contains("brown trout") => "Brown Trout",
            var s when s.Contains("rainbow trout") || s.Contains("steelhead") => "Rainbow Trout",
            var s when s.Contains("trout") => "Lake Trout",
            var s when s.Contains("muskie") || s.Contains("muskellunge") => "Muskie",
            var s when s.Contains("yellow perch") || s.Contains("perch") => "Yellow Perch",
            var s when s.Contains("bluegill") => "Bluegill",
            var s when s.Contains("sunfish") => "Bluegill",
            var s when s.Contains("black crappie") || s.Contains("white crappie") || s.Contains("crappie") => "Crappie",
            var s when s.Contains("salmon") => "Salmon",
            var s when s.Contains("channel catfish") => "Channel Catfish",
            var s when s.Contains("flathead catfish") => "Flathead Catfish",
            var s when s.Contains("catfish") => "Channel Catfish",
            var s when s.Contains("burbot") || s.Contains("eelpout") => "Burbot",
            var s when s.Contains("cisco") || s.Contains("tullibee") => "Cisco",
            var s when s.Contains("whitefish") => "Lake Whitefish",
            var s when s.Contains("northern") => "Northern Pike", // Catch "northern" alone
            _ => string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized.Length > 2 ? normalized : string.Empty
        };
    }

    public static string ExtractSpeciesFromContext(string text)
    {
        // Expanded species list with common variations
        var speciesVariations = new Dictionary<string, string[]>
        {
            ["Walleye"] = new[] { "walleye", "walleyes" },
            ["Northern Pike"] = new[] { "northern pike", "pike", "n. pike", "n pike" },
            ["Largemouth Bass"] = new[] { "largemouth bass", "largemouth", "lm bass", "l.m. bass" },
            ["Smallmouth Bass"] = new[] { "smallmouth bass", "smallmouth", "sm bass", "s.m. bass" },
            ["Lake Trout"] = new[] { "lake trout", "lakers", "lake trout" },
            ["Brook Trout"] = new[] { "brook trout", "brookies", "brook" },
            ["Brown Trout"] = new[] { "brown trout", "browns" },
            ["Rainbow Trout"] = new[] { "rainbow trout", "rainbows", "steelhead" },
            ["Muskie"] = new[] { "muskie", "muskellunge", "musky", "muskies" },
            ["Yellow Perch"] = new[] { "yellow perch", "perch" },
            ["Bluegill"] = new[] { "bluegill", "sunfish", "gills" },
            ["Crappie"] = new[] { "crappie", "crappies", "black crappie", "white crappie" },
            ["Channel Catfish"] = new[] { "channel catfish", "catfish", "cats" },
            ["Flathead Catfish"] = new[] { "flathead catfish", "flathead" },
            ["Burbot"] = new[] { "burbot", "eelpout" },
            ["Cisco"] = new[] { "cisco", "tullibee" },
            ["Lake Whitefish"] = new[] { "whitefish", "lake whitefish" },
            ["Salmon"] = new[] { "salmon", "coho", "chinook" }
        };
        
        var textLower = text.ToLowerInvariant();
        
        // Return the first species found (prioritize more specific matches)
        var foundSpecies = new List<string>();
        
        foreach (var (speciesName, variations) in speciesVariations)
        {
            foreach (var variation in variations)
            {
                if (textLower.Contains(variation))
                {
                    foundSpecies.Add(speciesName);
                    break; // Found this species, move to next species type
                }
            }
        }
        
        // Return the most specific species found (prefer longer/more specific names)
        return foundSpecies.OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;
    }

    public static List<string> ExtractAllSpeciesFromContext(string text)
    {
        // Extract ALL species mentioned in the text, not just the first one
        var speciesVariations = new Dictionary<string, string[]>
        {
            ["Walleye"] = new[] { "walleye", "walleyes" },
            ["Northern Pike"] = new[] { "northern pike", "pike", "n. pike", "n pike" },
            ["Largemouth Bass"] = new[] { "largemouth bass", "largemouth", "lm bass", "l.m. bass" },
            ["Smallmouth Bass"] = new[] { "smallmouth bass", "smallmouth", "sm bass", "s.m. bass" },
            ["Lake Trout"] = new[] { "lake trout", "lakers", "lake trout" },
            ["Brook Trout"] = new[] { "brook trout", "brookies", "brook" },
            ["Brown Trout"] = new[] { "brown trout", "browns" },
            ["Rainbow Trout"] = new[] { "rainbow trout", "rainbows", "steelhead" },
            ["Muskie"] = new[] { "muskie", "muskellunge", "musky", "muskies" },
            ["Yellow Perch"] = new[] { "yellow perch", "perch" },
            ["Bluegill"] = new[] { "bluegill", "sunfish", "gills" },
            ["Crappie"] = new[] { "crappie", "crappies", "black crappie", "white crappie" },
            ["Channel Catfish"] = new[] { "channel catfish", "catfish", "cats" },
            ["Flathead Catfish"] = new[] { "flathead catfish", "flathead" },
            ["Burbot"] = new[] { "burbot", "eelpout" },
            ["Cisco"] = new[] { "cisco", "tullibee" },
            ["Lake Whitefish"] = new[] { "whitefish", "lake whitefish" },
            ["Salmon"] = new[] { "salmon", "coho", "chinook" }
        };
        
        var textLower = text.ToLowerInvariant();
        var foundSpecies = new HashSet<string>();
        
        foreach (var (speciesName, variations) in speciesVariations)
        {
            foreach (var variation in variations)
            {
                if (textLower.Contains(variation))
                {
                    foundSpecies.Add(speciesName);
                    break; // Found this species, move to next species type
                }
            }
        }
        
        return foundSpecies.ToList();
    }

    public static bool IsValidWaterBodyEntry(string name, string county)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(county))
            return false;

        if (name.Length < 3 || name.Length > 150) // Increased max length
            return false;

        if (county.Length < 3 || county.Length > 50) // Increased max length for compound counties
            return false;

        // More targeted exclusion patterns - only exclude obvious non-water-body entries
        var excludePatterns = new[]
        {
            "SPECIES SEASONS", "POSSESSION LIMIT", "DAILY LIMIT", "SIZE LIMIT", 
            "OPEN SEASON", "CLOSED SEASON", "ANGLING ZONE", "SPECIAL STAMP",
            "DNR REGULATIONS", "FISHING LICENSE", "BORDER WATERS"
        };

        foreach (var pattern in excludePatterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Allow "Statewide" as a special water body name
        if (name.Equals("Statewide", StringComparison.OrdinalIgnoreCase))
            return true;

        // More permissive county validation - allow compound county names
        var invalidCountyPatterns = new[] { "ONLY STREAMS", "RIVERS ONLY", "LAKES ONLY", "BORDER ONLY" };
        foreach (var pattern in invalidCountyPatterns)
        {
            if (county.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string CleanWaterBodyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        name = Regex.Replace(name, @"\s+", " ").Trim();
        name = name.TrimEnd('.');

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
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

    public static string CleanCountyName(string county)
    {
        if (string.IsNullOrWhiteSpace(county))
            return string.Empty;

        county = county.Trim();

        if (county.EndsWith("County", StringComparison.OrdinalIgnoreCase))
        {
            county = county[..^6].Trim();
        }

        county = Regex.Replace(county, @"\s+", " ").Trim();
        county = county.TrimEnd('.');

        var words = county.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1)
            {
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

    public static string DetermineWaterType(string name)
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

        return "lake";
    }

    #endregion

    private static List<WaterBodyInfo> ExtractWaterBodiesWithRegex(string text)
    {
        var waterBodies = new List<WaterBodyInfo>();
        var seenCombinations = new HashSet<string>();

        var primaryPattern = @"([A-Z][A-Z\s\-,&\.''\d]+?)\s*\(([A-Za-z\s\.]+)\)";
        var matches = Regex.Matches(text, primaryPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var name = CleanWaterBodyName(match.Groups[1].Value.Trim());
            var county = CleanCountyName(match.Groups[2].Value.Trim());

            if (IsValidWaterBodyEntry(name, county))
            {
                var waterType = DetermineWaterType(name);
                var key = $"{name}|{county}".ToLowerInvariant();

                if (!seenCombinations.Contains(key))
                {
                    waterBodies.Add(new WaterBodyInfo
                    {
                        Name = name,
                        County = county,
                        WaterType = waterType,
                        State = "Minnesota"
                    });
                    
                    seenCombinations.Add(key);
                }
            }
        }

        return waterBodies.OrderBy(wb => wb.County).ThenBy(wb => wb.Name).ToList();
    }

    private static WaterBody? FindMatchingWaterBody(string name, string county, List<WaterBody> waterBodies)
    {
        return waterBodies.FirstOrDefault(wb => 
            string.Equals(wb.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(wb.County?.Name, county, StringComparison.OrdinalIgnoreCase));
    }

    private static FishSpecies? FindMatchingSpecies(string speciesName, List<FishSpecies> fishSpecies)
    {
        return fishSpecies.FirstOrDefault(fs => 
            string.Equals(fs.CommonName, speciesName, StringComparison.OrdinalIgnoreCase));
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

    private static List<FishingRegulationInfo> ParseRegulationText(string regulationText, string waterBodyName, string county)
    {
        var regulations = new List<FishingRegulationInfo>();
        
        // Split the regulation text into segments that might contain different species
        var segments = SplitSpeciesSegments(regulationText);
        
        foreach (var segment in segments)
        {
            // More aggressive patterns for daily limit extraction
            var dailyLimitPatterns = new[]
            {
                @"daily\s*limit\s*(?:for\s*)?([a-z\s]+?)\s*(?:to\s*|of\s*|is\s*)(\d+)", // "daily limit for species to X"
                @"reduces?\s*(?:the\s*)?daily\s*limit\s*(?:for\s*)?([a-z\s]+?)\s*(?:to\s*|of\s*)(\d+)", // "reduces daily limit for species to X"
                @"([a-z\s]+?)\s*daily\s*limit\s*(?:of\s*|is\s*)?(\d+)", // "species daily limit X"
                @"daily\s*limit\s*(?:to\s*|of\s*|is\s*)(\d+).*?(?:for\s*)?([a-z\s]+)", // "daily limit to X for species"
                @"daily\s*limits?\s*(?:of\s*)?(\d+).*?(?:for\s*)?([a-z\s]+)", // "daily limits of X for species"
            };

            foreach (var pattern in dailyLimitPatterns)
            {
                var matches = Regex.Matches(segment, pattern, RegexOptions.IgnoreCase);
                
                foreach (Match match in matches)
                {
                    string speciesText, limitText;
                    
                    // Handle different group orders based on pattern
                    if (pattern.Contains(@"(\d+).*?([a-z\s]+)"))
                    {
                        limitText = match.Groups[1].Value.Trim();
                        speciesText = match.Groups[2].Value.Trim();
                    }
                    else
                    {
                        speciesText = match.Groups[1].Value.Trim();
                        limitText = match.Groups[2].Value.Trim();
                    }
                    
                    if (int.TryParse(limitText, out var limit) && limit > 0 && limit < 100)
                    {
                        var speciesName = MapSpeciesName(speciesText);
                        if (!string.IsNullOrEmpty(speciesName))
                        {
                            regulations.Add(new FishingRegulationInfo
                            {
                                WaterBodyName = waterBodyName,
                                County = county,
                                SpeciesName = speciesName,
                                DailyLimit = limit,
                                Notes = segment
                            });
                        }
                    }
                }
            }

            // Look for possession limits in this segment
            var possessionMatches = Regex.Matches(segment, @"possession\s*limit\s*(?:is\s*)?(\d+)", RegexOptions.IgnoreCase);
            foreach (Match possessionMatch in possessionMatches)
            {
                if (int.TryParse(possessionMatch.Groups[1].Value, out var possessionLimit))
                {
                    var species = ExtractSpeciesFromContext(segment);
                    if (!string.IsNullOrEmpty(species))
                    {
                        regulations.Add(new FishingRegulationInfo
                        {
                            WaterBodyName = waterBodyName,
                            County = county,
                            SpeciesName = species,
                            PossessionLimit = possessionLimit,
                            Notes = segment
                        });
                    }
                }
            }

            // If no specific regulations found in this segment, try to extract any species mentioned
            if (!regulations.Any(r => r.Notes == segment))
            {
                var allSpecies = ExtractAllSpeciesFromContext(segment);
                foreach (var species in allSpecies)
                {
                    regulations.Add(new FishingRegulationInfo
                    {
                        WaterBodyName = waterBodyName,
                        County = county,
                        SpeciesName = species,
                        Notes = segment
                    });
                }
            }
        }

        return regulations;
    }
}

/// <summary>
/// Information about a fishing regulation extracted from text
/// </summary>
public class FishingRegulationInfo
{
    public string WaterBodyName { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string SpeciesName { get; set; } = string.Empty;
    
    // Season information
    public int? SeasonStartMonth { get; set; }
    public int? SeasonStartDay { get; set; }
    public int? SeasonEndMonth { get; set; }
    public int? SeasonEndDay { get; set; }
    
    // Bag limits
    public int? DailyLimit { get; set; }
    public int? PossessionLimit { get; set; }
    
    // Size limits
    public decimal? MinimumSizeInches { get; set; }
    public decimal? MaximumSizeInches { get; set; }
    public decimal? ProtectedSlotMinInches { get; set; }
    public decimal? ProtectedSlotMaxInches { get; set; }
    
    // Special regulations
    public List<string> SpecialRegulations { get; set; } = new();
    public List<string> RequiredStamps { get; set; } = new();
    
    // Catch and release
    public bool IsCatchAndRelease { get; set; } = false;
    
    // Additional notes
    public string? Notes { get; set; }
}