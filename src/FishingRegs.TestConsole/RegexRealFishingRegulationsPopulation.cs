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
using Npgsql;

namespace FishingRegs.TestConsole;

/// <summary>
/// Regex-based fishing regulations extraction to REAL PostgreSQL database
/// </summary>
class RegexRealFishingRegulationsPopulation
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunRegexRealFishingRegulationsPopulation(string[] args)
    {
        AnsiConsole.Write(
            new Panel(new Text("REAL Database Fishing Regulations Population", style: "bold"))
                .BorderColor(Color.Red)
                .Header("[red]?? PRODUCTION DATABASE ??[/]")
                .Padding(1, 0));

        AnsiConsole.MarkupLine("[red]?? This will populate fishing regulations to the REAL PostgreSQL database![/]");
        AnsiConsole.MarkupLine("[yellow]This operation will create actual fishing regulation records.[/]");
        AnsiConsole.WriteLine();

        // Safety confirmation
        var confirmed = AnsiConsole.Confirm("[red]Are you absolutely sure you want to proceed with REAL database population?[/]");

        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled. Good choice for safety![/]");
            return;
        }

        AnsiConsole.MarkupLine("[red]Proceeding with REAL database population...[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Setup services with REAL PostgreSQL
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            var configuration = BuildConfiguration();
            services.AddSingleton<IConfiguration>(configuration);

            // Get PostgreSQL connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                AnsiConsole.MarkupLine("[red]? No PostgreSQL connection string found![/]");
                AnsiConsole.MarkupLine("[yellow]Make sure 'DefaultConnection' is configured in appsettings.json or user secrets.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[yellow]Database connection:[/] {MaskConnectionString(connectionString)}");

            // Test database connection
            AnsiConsole.MarkupLine("[yellow]Testing database connection...[/]");
            
            try
            {
                using var testConnection = new NpgsqlConnection(connectionString);
                await testConnection.OpenAsync();
                AnsiConsole.MarkupLine("[green]? Database connection successful[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]? Database connection failed:[/] {ex.Message}");
                return;
            }

            // Add Entity Framework with PostgreSQL
            services.AddDbContext<FishingRegsDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                options.EnableSensitiveDataLogging(false); // Disable for production
                options.EnableDetailedErrors();
            });

            var serviceProvider = services.BuildServiceProvider();
            var dbContext = serviceProvider.GetRequiredService<FishingRegsDbContext>();
            var logger = serviceProvider.GetRequiredService<ILogger<RegexRealFishingRegulationsPopulation>>();

            // Verify database schema exists
            AnsiConsole.MarkupLine("[yellow]Verifying database schema...[/]");
            
            try
            {
                var stateCount = await dbContext.States.CountAsync();
                var speciesCount = await dbContext.FishSpecies.CountAsync();
                var waterBodyCount = await dbContext.WaterBodies.CountAsync();
                
                AnsiConsole.MarkupLine($"[green]? Database ready - States: {stateCount}, Species: {speciesCount}, Water Bodies: {waterBodyCount}[/]");
                
                if (waterBodyCount == 0)
                {
                    AnsiConsole.MarkupLine("[red]? No water bodies found in database![/]");
                    AnsiConsole.MarkupLine("[yellow]Run water body population first.[/]");
                    return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]? Database schema verification failed:[/] {ex.Message}");
                return;
            }

            // Load the fishing regulations file
            var testTextPath = @"s:\src\rdl\BlazorFishingRegs\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                AnsiConsole.MarkupLine($"[red]? Test file not found:[/] {testTextPath}");
                return;
            }

            var textContent = await File.ReadAllTextAsync(testTextPath);
            AnsiConsole.MarkupLine($"[green]? Loaded document:[/] {textContent.Length:N0} characters");

            // Step 1: Extract fishing regulations
            AnsiConsole.Write(new Rule("[blue]Step 1: Extract Fishing Regulations[/]"));
            var regulations = ExtractFishingRegulations(textContent);
            AnsiConsole.MarkupLine($"[green]? Extracted {regulations.Count} regulation entries[/]");

            if (regulations.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]?? No fishing regulations found to populate![/]");
                return;
            }

            // Step 2: Populate fishing regulations
            AnsiConsole.Write(new Rule("[blue]Step 2: Populate Real Database[/]"));
            await PopulateFishingRegulations(dbContext, regulations, logger);

            // Step 3: Verify and display results
            AnsiConsole.Write(new Rule("[blue]Step 3: Verification & Results[/]"));
            await VerifyAndDisplayResults(dbContext);

            AnsiConsole.MarkupLine($"\n[green]?? REAL database fishing regulations population completed![/]");
            AnsiConsole.MarkupLine("[red]?? Changes have been made to the production database.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    private static async Task PopulateFishingRegulations(FishingRegsDbContext dbContext, List<FishingRegulationInfo> regulations, ILogger logger)
    {
        // Get all water bodies and fish species from database
        var waterBodies = await dbContext.WaterBodies
            .Include(wb => wb.County)
            .Include(wb => wb.State)
            .ToListAsync();

        var fishSpecies = await dbContext.FishSpecies.ToListAsync();

        AnsiConsole.MarkupLine($"[dim]Database contains {waterBodies.Count} water bodies and {fishSpecies.Count} fish species[/]");

        var createdRegulations = 0;
        var skippedRegulations = 0;
        var errorCount = 0;
        var currentYear = DateTime.Now.Year;

        var progressTask = AnsiConsole.Progress().Start(ctx =>
        {
            var task = ctx.AddTask("[green]Processing regulations[/]");
            task.MaxValue = regulations.Count;

            foreach (var regInfo in regulations)
            {
                try
                {
                    // Match water body
                    var matchedWaterBody = FindMatchingWaterBody(regInfo.WaterBodyName, regInfo.County, waterBodies);
                    if (matchedWaterBody == null)
                    {
                        logger.LogDebug($"No matching water body found for: {regInfo.WaterBodyName} ({regInfo.County})");
                        skippedRegulations++;
                        task.Increment(1);
                        continue;
                    }

                    // Match fish species
                    var matchedSpecies = FindMatchingSpecies(regInfo.SpeciesName, fishSpecies);
                    if (matchedSpecies == null)
                    {
                        logger.LogDebug($"No matching species found for: {regInfo.SpeciesName}");
                        skippedRegulations++;
                        task.Increment(1);
                        continue;
                    }

                    // Check if regulation already exists
                    var existingRegulation = dbContext.FishingRegulations
                        .FirstOrDefault(fr => 
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
                            
                            // Season information
                            SeasonStartMonth = regInfo.SeasonStartMonth,
                            SeasonStartDay = regInfo.SeasonStartDay,
                            SeasonEndMonth = regInfo.SeasonEndMonth,
                            SeasonEndDay = regInfo.SeasonEndDay,
                            
                            // Bag limits
                            DailyLimit = regInfo.DailyLimit,
                            PossessionLimit = regInfo.PossessionLimit,
                            
                            // Size limits
                            MinimumSizeInches = regInfo.MinimumSizeInches,
                            MaximumSizeInches = regInfo.MaximumSizeInches,
                            ProtectedSlotMinInches = regInfo.ProtectedSlotMinInches,
                            ProtectedSlotMaxInches = regInfo.ProtectedSlotMaxInches,
                            
                            // Special regulations
                            SpecialRegulations = regInfo.SpecialRegulations,
                            RequiredStamps = regInfo.RequiredStamps,
                            
                            // Catch and release
                            IsCatchAndRelease = regInfo.IsCatchAndRelease,
                            
                            // Notes
                            Notes = regInfo.Notes,
                            
                            // Metadata
                            IsActive = true,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        dbContext.FishingRegulations.Add(newRegulation);
                        createdRegulations++;

                        // Save in batches
                        if (createdRegulations % 50 == 0)
                        {
                            var saved = dbContext.SaveChanges();
                            task.Description = $"[green]Saved {createdRegulations} regulations (batch: {saved})[/]";
                        }
                    }
                    else
                    {
                        skippedRegulations++;
                        logger.LogDebug($"Regulation already exists for {matchedWaterBody.Name} - {matchedSpecies.CommonName}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logger.LogError(ex, $"Error processing regulation for {regInfo.WaterBodyName} - {regInfo.SpeciesName}");
                }

                task.Increment(1);
            }

            return Task.CompletedTask;
        });

        // Save any remaining changes
        var finalSaved = await dbContext.SaveChangesAsync();
        
        AnsiConsole.MarkupLine($"[green]? Created {createdRegulations} fishing regulations[/]");
        AnsiConsole.MarkupLine($"[yellow]?? Skipped {skippedRegulations} regulations (no match found)[/]");
        
        if (errorCount > 0)
        {
            AnsiConsole.MarkupLine($"[red]? Errors processing {errorCount} regulations[/]");
        }
    }

    private static async Task VerifyAndDisplayResults(FishingRegsDbContext dbContext)
    {
        try
        {
            var totalRegulations = await dbContext.FishingRegulations.CountAsync();
            var totalWaterBodies = await dbContext.WaterBodies.CountAsync();
            var totalSpecies = await dbContext.FishSpecies.CountAsync();

            var waterBodiesWithRegulations = await dbContext.WaterBodies
                .Where(wb => wb.FishingRegulations.Any())
                .CountAsync();

            var speciesWithRegulations = await dbContext.FishSpecies
                .Where(fs => fs.FishingRegulations.Any())
                .CountAsync();

            var summaryTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Count")
                .AddColumn("Percentage");

            summaryTable.AddRow("Total Fishing Regulations", totalRegulations.ToString(), "100%");
            summaryTable.AddRow("Water Bodies with Regulations", waterBodiesWithRegulations.ToString(), 
                $"{(double)waterBodiesWithRegulations / totalWaterBodies * 100:F1}%");
            summaryTable.AddRow("Species with Regulations", speciesWithRegulations.ToString(), 
                $"{(double)speciesWithRegulations / totalSpecies * 100:F1}%");

            AnsiConsole.Write(summaryTable);

            // Show sample regulations
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Sample Fishing Regulations from Real Database:[/]");

            var sampleRegulations = await dbContext.FishingRegulations
                .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.County)
                .Include(fr => fr.Species)
                .OrderBy(fr => fr.WaterBody.Name)
                .Take(10)
                .ToListAsync();

            foreach (var reg in sampleRegulations)
            {
                AnsiConsole.MarkupLine($"• [blue]{reg.WaterBody.Name}[/] ([yellow]{reg.WaterBody.County?.Name}[/]) - [green]{reg.Species.CommonName}[/]");
                
                if (reg.DailyLimit.HasValue)
                    AnsiConsole.MarkupLine($"  Daily Limit: {reg.DailyLimit}");
                
                if (reg.MinimumSizeInches.HasValue)
                    AnsiConsole.MarkupLine($"  Min Size: {reg.MinimumSizeInches}\"");
                
                if (reg.SeasonStartMonth.HasValue)
                {
                    var startMonth = GetMonthName(reg.SeasonStartMonth.Value);
                    var endMonth = GetMonthName(reg.SeasonEndMonth ?? 12);
                    AnsiConsole.MarkupLine($"  Season: {startMonth} {reg.SeasonStartDay ?? 1} - {endMonth} {reg.SeasonEndDay ?? 31}");
                }
                
                if (reg.IsCatchAndRelease)
                    AnsiConsole.MarkupLine($"  [red]Catch & Release Only[/]");
                
                if (reg.SpecialRegulations.Any())
                    AnsiConsole.MarkupLine($"  Special: {string.Join(", ", reg.SpecialRegulations)}");
                
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]? Error verifying results:[/] {ex.Message}");
        }
    }

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMM");
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask sensitive parts of connection string for display
        var masked = connectionString;
        var passwordMatch = Regex.Match(masked, @"Password=([^;]+)", RegexOptions.IgnoreCase);
        if (passwordMatch.Success)
        {
            masked = masked.Replace(passwordMatch.Groups[1].Value, "****");
        }
        return masked;
    }

    private static WaterBody? FindMatchingWaterBody(string name, string county, List<WaterBody> waterBodies)
    {
        // Try exact match first
        var exact = waterBodies.FirstOrDefault(wb => 
            string.Equals(wb.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(wb.County?.Name, county, StringComparison.OrdinalIgnoreCase));
        
        if (exact != null) return exact;

        // Try fuzzy match (contains)
        var fuzzy = waterBodies.FirstOrDefault(wb => 
            wb.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(wb.County?.Name, county, StringComparison.OrdinalIgnoreCase));
        
        return fuzzy;
    }

    private static FishSpecies? FindMatchingSpecies(string speciesName, List<FishSpecies> fishSpecies)
    {
        // Try exact match first
        var exact = fishSpecies.FirstOrDefault(fs => 
            string.Equals(fs.CommonName, speciesName, StringComparison.OrdinalIgnoreCase));
        
        if (exact != null) return exact;

        // Try partial matches for common variations
        var normalizedName = speciesName.ToLowerInvariant();
        
        if (normalizedName.Contains("walleye")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Walleye"));
        if (normalizedName.Contains("pike") && !normalizedName.Contains("bass")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Pike"));
        if (normalizedName.Contains("bass")) 
        {
            if (normalizedName.Contains("largemouth")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Largemouth"));
            if (normalizedName.Contains("smallmouth")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Smallmouth"));
            return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Bass"));
        }
        if (normalizedName.Contains("trout")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Trout"));
        if (normalizedName.Contains("muskie") || normalizedName.Contains("muskellunge")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Muskie"));
        if (normalizedName.Contains("perch")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Perch"));
        if (normalizedName.Contains("bluegill")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Bluegill"));
        if (normalizedName.Contains("crappie")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Crappie"));
        if (normalizedName.Contains("salmon")) return fishSpecies.FirstOrDefault(fs => fs.CommonName.Contains("Salmon"));

        return null;
    }

    #region Helper Methods (reused from extraction class)

    // Include all the helper methods from the original extraction class
    private static List<FishingRegulationInfo> ExtractFishingRegulations(string text) => RegexFishingRegulationsExtraction.ExtractFishingRegulations(text);
    private static bool IsValidWaterBodyEntry(string name, string county) => RegexFishingRegulationsExtraction.IsValidWaterBodyEntry(name, county);
    private static string CleanWaterBodyName(string name) => RegexFishingRegulationsExtraction.CleanWaterBodyName(name);
    private static string CleanCountyName(string county) => RegexFishingRegulationsExtraction.CleanCountyName(county);

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

    #endregion
}