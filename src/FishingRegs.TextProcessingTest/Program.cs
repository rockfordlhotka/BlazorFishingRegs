using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FishingRegs.TextProcessingTest;

/// <summary>
/// Console application to test basic text parsing of lake regulations without AI
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== FishingRegs Basic Text Parsing Test ===");
        Console.WriteLine();

        // Set up dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register basic parsing application
                services.AddSingleton<BasicParsingApplication>();
            })
            .Build();

        try
        {
            // Run the test application
            var app = host.Services.GetRequiredService<BasicParsingApplication>();
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
