using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Extensions;
using FishingRegs.Services.Interfaces;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple test for just the AI extraction part without database dependencies
/// </summary>
class SimpleAiExtractionTest
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task RunAiExtractionTest(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Simple AI Extraction Test", style: "bold"))
                .BorderColor(Color.Aqua)
                .Header("[yellow]No Database Dependencies[/]")
                .Padding(1, 0));

        // Setup dependency injection with minimal configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        try
        {
            // Build configuration
            var configuration = BuildConfiguration();
            services.AddSingleton<IConfiguration>(configuration);

            // Check if we have the required Azure OpenAI configuration
            var endpoint = configuration["AzureAI:OpenAI:Endpoint"];
            var apiKey = configuration["AzureAI:OpenAI:ApiKey"];
            var deploymentName = configuration["AzureAI:OpenAI:DeploymentName"];

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deploymentName))
            {
                AnsiConsole.Write(
                    new Panel(new Markup("[red]❌ Missing Azure OpenAI Configuration[/]\n\n" +
                        "[yellow]Please set up user secrets:[/]\n\n" +
                        "[grey]dotnet user-secrets set \"AzureAI:OpenAI:Endpoint\" \"https://your-openai.openai.azure.com/\"[/]\n" +
                        "[grey]dotnet user-secrets set \"AzureAI:OpenAI:ApiKey\" \"your-api-key\"[/]\n" +
                        "[grey]dotnet user-secrets set \"AzureAI:OpenAI:DeploymentName\" \"your-deployment-name\"[/]"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                    
                AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
                Console.ReadKey();
                return;
            }

            // Display configuration status
            var configTable = new Table()
                .AddColumn("Configuration")
                .AddColumn("Value");
            
            configTable.AddRow("Azure OpenAI Endpoint", $"[green]{endpoint}[/]");
            configTable.AddRow("Deployment Name", $"[green]{deploymentName}[/]");
            configTable.AddRow("API Key", $"[green]{(apiKey.Length > 8 ? apiKey.Substring(0, 8) + "..." : "***")}[/]");
            
            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            // Register only the AI extraction service
            services.AddScoped<IAiLakeRegulationExtractionService, FishingRegs.Services.Services.AiLakeRegulationExtractionService>();

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleAiExtractionTest>>();
            var aiExtractionService = serviceProvider.GetRequiredService<IAiLakeRegulationExtractionService>();

            // Test with the fishing regulations text file
            var testTextPath = @"s:\src\rdl\BlazorAI-spec\data\fishing_regs.txt";
            
            if (!File.Exists(testTextPath))
            {
                logger.LogError("Test text file not found at: {FilePath}", testTextPath);
                AnsiConsole.MarkupLine($"[red]❌ Test text file not found at:[/] {testTextPath}");
                return;
            }

            logger.LogInformation("Found test text file at: {FilePath}", testTextPath);
            AnsiConsole.MarkupLine($"[green]📄 Processing text file:[/] {testTextPath}");

            // Read the text file
            var textContent = await File.ReadAllTextAsync(testTextPath);
            AnsiConsole.MarkupLine($"[blue]📊 File size:[/] {textContent.Length:N0} characters");

            // Test the AI extraction
            AnsiConsole.Write(new Rule("[blue]AI Extraction[/]"));
            AnsiConsole.MarkupLine("[blue]🤖 Starting AI extraction...[/]");
            
            var extractionResult = await AnsiConsole.Status()
                .Start("Processing with Azure OpenAI...", async ctx => 
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    return await aiExtractionService.ExtractLakeRegulationsAsync(textContent);
                });

            if (!extractionResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]❌ AI extraction failed:[/] {extractionResult.ErrorMessage}");
                return;
            }

            AnsiConsole.MarkupLine("[green]✅ AI extraction completed successfully![/]");
            
            // Create results table
            var resultsTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Value");
            
            resultsTable.AddRow("Total lakes processed", extractionResult.TotalLakesProcessed.ToString());
            resultsTable.AddRow("Regulations extracted", extractionResult.TotalRegulationsExtracted.ToString());
            resultsTable.AddRow("Processing time", $"{extractionResult.ProcessingTime.TotalSeconds:F2} seconds");
            
            if (extractionResult.ProcessingWarnings.Any())
            {
                resultsTable.AddRow("Warnings", extractionResult.ProcessingWarnings.Count.ToString());
            }
            
            AnsiConsole.Write(resultsTable);

            if (extractionResult.ProcessingWarnings.Any())
            {
                AnsiConsole.MarkupLine("\n[yellow]⚠️ Processing warnings:[/]");
                foreach (var warning in extractionResult.ProcessingWarnings.Take(3))
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning}[/]");
                }
            }

            // Show detailed results
            AnsiConsole.Write(new Rule("[blue]Extracted Regulations[/]"));
            AnsiConsole.MarkupLine($"[cyan]📋 Extracted {extractionResult.ExtractedRegulations.Count} lake regulations:[/]");
            
            foreach (var (lake, index) in extractionResult.ExtractedRegulations.Select((l, i) => (l, i)).Take(10))
            {
                AnsiConsole.MarkupLine($"\n  [cyan]{index + 1}. {lake.LakeName}[/] ([dim]{lake.County} County[/])");
                AnsiConsole.MarkupLine($"     [dim]Regulations: {lake.Regulations.SpecialRegulations.Count}[/]");
                AnsiConsole.MarkupLine($"     [dim]Experimental: {lake.Regulations.IsExperimental}[/]");
                
                foreach (var regulation in lake.Regulations.SpecialRegulations.Take(2))
                {
                    var details = new List<string>();
                    if (regulation.DailyLimit.HasValue) details.Add($"Daily limit: {regulation.DailyLimit}");
                    if (!string.IsNullOrEmpty(regulation.MinimumSize)) details.Add($"Min size: {regulation.MinimumSize}");
                    if (!string.IsNullOrEmpty(regulation.MaximumSize)) details.Add($"Max size: {regulation.MaximumSize}");
                    if (regulation.CatchAndRelease) details.Add("Catch & release");
                    
                    Console.WriteLine($"       • {regulation.Species}: {regulation.RegulationType} - {string.Join(", ", details)}");
                    if (!string.IsNullOrEmpty(regulation.Notes))
                    {
                        Console.WriteLine($"         Notes: {regulation.Notes.Substring(0, Math.Min(100, regulation.Notes.Length))}...");
                    }
                }
                
                if (lake.Regulations.SpecialRegulations.Count > 2)
                {
                    Console.WriteLine($"       ... and {lake.Regulations.SpecialRegulations.Count - 2} more regulations");
                }
            }

            if (extractionResult.ExtractedRegulations.Count > 10)
            {
                AnsiConsole.MarkupLine($"\n  [dim]... and {extractionResult.ExtractedRegulations.Count - 10} more lakes[/]");
            }

            // Test individual lake extraction
            if (extractionResult.ExtractedRegulations.Any())
            {
                AnsiConsole.Write(new Rule("[blue]Individual Lake Test[/]"));
                AnsiConsole.MarkupLine("[blue]🔍 Testing individual lake extraction...[/]");
                var firstLake = extractionResult.ExtractedRegulations.First();
                var testText = $"{firstLake.LakeName} ({firstLake.County}) Sample regulation text for testing";
                
                var singleResult = await AnsiConsole.Status()
                    .Start("Testing single lake extraction...", async ctx => 
                    {
                        ctx.Spinner(Spinner.Known.BouncingBall);
                        ctx.SpinnerStyle(Style.Parse("green"));
                        return await aiExtractionService.ExtractSingleLakeRegulationAsync(
                            testText, firstLake.LakeName, firstLake.County);
                    });
                
                if (singleResult != null)
                {
                    AnsiConsole.MarkupLine($"[green]✅ Individual extraction successful for {singleResult.LakeName}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]❌ Individual extraction returned null[/]");
                }
            }

            // Final success message
            AnsiConsole.Write(
                new Panel(new Text("🎉 AI Extraction Test Completed! 🎉", style: "bold green"))
                    .BorderColor(Color.Green)
                    .Padding(1, 0));
        }
        catch (Exception ex)
        {
            AnsiConsole.Write(
                new Panel(new Text("Error", style: "bold red"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                    
            AnsiConsole.MarkupLine($"[red]❌ Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Stack trace: {ex.StackTrace}[/]");
        }
        
        AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
        Console.ReadKey();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        // Always add user secrets in Development
        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }
}
