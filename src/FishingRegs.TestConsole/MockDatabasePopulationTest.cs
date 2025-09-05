using FishingRegs.Data;
using FishingRegs.Services.Models;
using FishingRegs.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Text.Json;

namespace FishingRegs.TestConsole;

/// <summary>
/// Test program for database population using pre-extracted or mock data
/// This allows testing without making OpenAI API calls every time
/// </summary>
public static class MockDatabasePopulationTest
{
    public static async Task RunMockDatabaseTest(string[] args)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]Mock Database Population Test[/]");
            AnsiConsole.WriteLine();

            // Choose data source
            var dataSource = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose [green]data source[/]:")
                    .AddChoices(new[] {
                        "Pre-extracted JSON file (Lake Superior)",
                        "Test JSON file (Proper format)",
                        "Generate mock data",
                        "Cancel"
                    }));

            if (dataSource == "Cancel") return;

            using var services = CreateServiceProvider();
            var dbContext = services.GetRequiredService<FishingRegsDbContext>();
            var populationService = services.GetRequiredService<RegulationDatabasePopulationService>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            // Ensure database is created
            await dbContext.Database.EnsureCreatedAsync();

            AiLakeRegulationExtractionResult extractionResult;

            if (dataSource == "Pre-extracted JSON file (Lake Superior)")
            {
                extractionResult = await LoadFromJsonFile("lake-superior.json");
            }
            else if (dataSource == "Test JSON file (Proper format)")
            {
                extractionResult = await LoadFromProperJsonFile("test-lakes.json");
            }
            else
            {
                extractionResult = GenerateMockData();
            }

            if (extractionResult == null || !extractionResult.IsSuccess)
            {
                AnsiConsole.MarkupLine("[red]Failed to load data![/]");
                return;
            }

            // Display what we're about to populate
            AnsiConsole.MarkupLine($"[green]Data loaded successfully![/]");
            AnsiConsole.MarkupLine($"[cyan]Lakes to process: {extractionResult.TotalLakesProcessed}[/]");
            AnsiConsole.MarkupLine($"[cyan]Total regulations: {extractionResult.TotalRegulationsExtracted}[/]");
            AnsiConsole.WriteLine();

            foreach (var lake in extractionResult.ExtractedRegulations)
            {
                AnsiConsole.MarkupLine($"[yellow]Lake:[/] {lake.LakeName} (ID: {lake.LakeId})");
                AnsiConsole.MarkupLine($"[yellow]County:[/] {lake.County}");
                AnsiConsole.MarkupLine($"[yellow]Special Regulations:[/] {lake.Regulations.SpecialRegulations.Count}");
                AnsiConsole.WriteLine();
            }

            if (!AnsiConsole.Confirm("Proceed with database population?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return;
            }

            // Create a dummy source document
            var sourceDocumentId = Guid.NewGuid();
            var currentYear = DateTime.Now.Year;

            AnsiConsole.MarkupLine("[green]Starting database population...[/]");

            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Populating database[/]");
                    task.MaxValue = 100;

                    try
                    {
                        await populationService.PopulateDatabaseAsync(
                            extractionResult, 
                            sourceDocumentId, 
                            currentYear, 
                            CancellationToken.None);

                        task.Value = 100;
                    }
                    catch (Exception)
                    {
                        task.StopTask();
                        throw;
                    }
                });

            AnsiConsole.MarkupLine("[green]✅ Database population completed successfully![/]");

            // Display summary
            await DisplayDatabaseSummary(dbContext);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine($"[red]❌ Database population failed: {ex.Message}[/]");
        }
    }

    private static async Task<AiLakeRegulationExtractionResult> LoadFromJsonFile(string fileName)
    {
        try
        {
            var jsonPath = Path.Combine(@"s:\src\rdl\BlazorAI-spec\data\extracted-regulations", fileName);

            if (!File.Exists(jsonPath))
            {
                AnsiConsole.MarkupLine($"[red]JSON file not found at: {jsonPath}[/]");
                return new AiLakeRegulationExtractionResult { IsSuccess = false, ErrorMessage = "File not found" };
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            
            // Parse the JSON and convert to our model format
            var jsonData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var result = ConvertJsonToAiResult(jsonData);

            AnsiConsole.MarkupLine($"[green]Loaded data from: {jsonPath}[/]");
            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading JSON file: {ex.Message}[/]");
            return new AiLakeRegulationExtractionResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<AiLakeRegulationExtractionResult> LoadFromProperJsonFile(string fileName)
    {
        try
        {
            var jsonPath = Path.Combine(@"s:\src\rdl\BlazorAI-spec\data\extracted-regulations", fileName);

            if (!File.Exists(jsonPath))
            {
                AnsiConsole.MarkupLine($"[red]JSON file not found at: {jsonPath}[/]");
                return new AiLakeRegulationExtractionResult { IsSuccess = false, ErrorMessage = "File not found" };
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            
            // Directly deserialize since this is in the proper format
            var result = JsonSerializer.Deserialize<AiLakeRegulationExtractionResult>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new AiLakeRegulationExtractionResult { IsSuccess = false, ErrorMessage = "Failed to deserialize JSON" };
            }

            AnsiConsole.MarkupLine($"[green]Loaded properly formatted data from: {jsonPath}[/]");
            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading JSON file: {ex.Message}[/]");
            return new AiLakeRegulationExtractionResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private static AiLakeRegulationExtractionResult ConvertJsonToAiResult(JsonElement jsonData)
    {
        var result = new AiLakeRegulationExtractionResult
        {
            IsSuccess = true,
            TotalLakesProcessed = 1,
            ProcessingTime = TimeSpan.FromSeconds(0)
        };

        var lakeRegulation = new AiLakeRegulation
        {
            LakeId = jsonData.GetProperty("lakeId").GetInt32(),
            LakeName = jsonData.GetProperty("lakeName").GetString() ?? "",
            County = "Cook" // Default since not in JSON
        };

        var regulations = jsonData.GetProperty("regulations");
        var specialRegulations = new List<AiSpecialRegulation>();

        // Convert bag limits to special regulations
        if (regulations.TryGetProperty("bagLimits", out var bagLimits))
        {
            foreach (var bagLimit in bagLimits.EnumerateArray())
            {
                specialRegulations.Add(new AiSpecialRegulation
                {
                    Species = bagLimit.GetProperty("species").GetString() ?? "",
                    RegulationType = AiRegulationType.DailyLimit,
                    DailyLimit = bagLimit.GetProperty("dailyLimit").GetInt32(),
                    PossessionLimit = bagLimit.GetProperty("possessionLimit").GetInt32(),
                    Notes = bagLimit.TryGetProperty("notes", out var notes) ? notes.GetString() ?? "" : ""
                });
            }
        }

        // Convert size limits to special regulations
        if (regulations.TryGetProperty("sizeLimits", out var sizeLimits))
        {
            foreach (var sizeLimit in sizeLimits.EnumerateArray())
            {
                specialRegulations.Add(new AiSpecialRegulation
                {
                    Species = sizeLimit.GetProperty("species").GetString() ?? "",
                    RegulationType = AiRegulationType.SizeLimit,
                    MinimumSize = sizeLimit.TryGetProperty("minimumSize", out var minSize) ? minSize.GetString() : null,
                    MaximumSize = sizeLimit.TryGetProperty("maximumSize", out var maxSize) ? maxSize.GetString() : null,
                    ProtectedSlot = sizeLimit.TryGetProperty("protectedSlot", out var slot) ? slot.GetString() : null,
                    Notes = sizeLimit.TryGetProperty("notes", out var notes) ? notes.GetString() ?? "" : ""
                });
            }
        }

        // Convert special regulations
        if (regulations.TryGetProperty("specialRegulations", out var specRegs))
        {
            foreach (var specReg in specRegs.EnumerateArray())
            {
                var applicableSpecies = specReg.TryGetProperty("applicableSpecies", out var species) 
                    ? species.EnumerateArray().Select(s => s.GetString() ?? "").ToList()
                    : new List<string> { "All" };

                foreach (var speciesName in applicableSpecies)
                {
                    specialRegulations.Add(new AiSpecialRegulation
                    {
                        Species = speciesName,
                        RegulationType = AiRegulationType.Combined,
                        Notes = $"{specReg.GetProperty("regulation").GetString()}: {(specReg.TryGetProperty("details", out var details) ? details.GetString() : "")}"
                    });
                }
            }
        }

        lakeRegulation.Regulations.SpecialRegulations = specialRegulations;
        lakeRegulation.Regulations.LastUpdated = DateTime.UtcNow;
        
        result.ExtractedRegulations.Add(lakeRegulation);
        result.TotalRegulationsExtracted = specialRegulations.Count;

        return result;
    }

    private static AiLakeRegulationExtractionResult GenerateMockData()
    {
        var result = new AiLakeRegulationExtractionResult
        {
            IsSuccess = true,
            TotalLakesProcessed = 2,
            ProcessingTime = TimeSpan.FromSeconds(0)
        };

        // Mock Lake 1: Test Lake Alpha
        var lake1 = new AiLakeRegulation
        {
            LakeId = 9001,
            LakeName = "Test Lake Alpha",
            County = "Mock County"
        };

        lake1.Regulations.SpecialRegulations = new List<AiSpecialRegulation>
        {
            new() {
                Species = "Walleye",
                RegulationType = AiRegulationType.DailyLimit,
                DailyLimit = 6,
                PossessionLimit = 12,
                Notes = "Mock walleye regulation"
            },
            new() {
                Species = "Walleye",
                RegulationType = AiRegulationType.SizeLimit,
                MinimumSize = "15 inches",
                Notes = "Mock walleye size limit"
            },
            new() {
                Species = "Northern Pike",
                RegulationType = AiRegulationType.DailyLimit,
                DailyLimit = 3,
                PossessionLimit = 6,
                Notes = "Mock northern pike regulation"
            }
        };

        // Mock Lake 2: Test Lake Beta
        var lake2 = new AiLakeRegulation
        {
            LakeId = 9002,
            LakeName = "Test Lake Beta",
            County = "Mock County"
        };

        lake2.Regulations.SpecialRegulations = new List<AiSpecialRegulation>
        {
            new() {
                Species = "Bass",
                RegulationType = AiRegulationType.DailyLimit,
                DailyLimit = 5,
                PossessionLimit = 10,
                Notes = "Mock bass regulation"
            },
            new() {
                Species = "Bass",
                RegulationType = AiRegulationType.SizeLimit,
                MinimumSize = "14 inches",
                Notes = "Mock bass size limit"
            },
            new() {
                Species = "Bluegill",
                RegulationType = AiRegulationType.DailyLimit,
                DailyLimit = 20,
                PossessionLimit = 40,
                Notes = "Mock bluegill regulation"
            }
        };

        result.ExtractedRegulations.AddRange(new[] { lake1, lake2 });
        result.TotalRegulationsExtracted = lake1.Regulations.SpecialRegulations.Count + 
                                          lake2.Regulations.SpecialRegulations.Count;

        AnsiConsole.MarkupLine("[green]Generated mock data for 2 test lakes[/]");
        return result;
    }

    private static async Task DisplayDatabaseSummary(FishingRegsDbContext dbContext)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Database Summary:[/]");
        
        var lakeCount = await dbContext.WaterBodies.CountAsync();
        var fishSpeciesCount = await dbContext.FishSpecies.CountAsync();
        var regulationCount = await dbContext.FishingRegulations.CountAsync();

        var table = new Table();
        table.AddColumn("Entity");
        table.AddColumn("Count");

        table.AddRow("Water Bodies", lakeCount.ToString());
        table.AddRow("Fish Species", fishSpeciesCount.ToString());
        table.AddRow("Regulations", regulationCount.ToString());

        AnsiConsole.Write(table);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Configure Entity Framework with in-memory database for testing
        services.AddDbContext<FishingRegsDbContext>(options =>
            options.UseInMemoryDatabase("TestDatabase"));

        // Register services
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<RegulationDatabasePopulationService>();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services.BuildServiceProvider();
    }
}
