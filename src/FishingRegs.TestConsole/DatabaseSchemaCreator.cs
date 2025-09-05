using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Spectre.Console;

namespace FishingRegs.TestConsole;

/// <summary>
/// Simple utility to create the database schema
/// </summary>
class DatabaseSchemaCreator
{
    private const string UserSecretsId = "7d5de198-3095-4d2d-acda-c2631c63e9b6";

    public static async Task CreateSchema(string[] args)
    {
        // Create a header panel
        AnsiConsole.Write(
            new Panel(new Text("Database Schema Creator", style: "bold"))
                .BorderColor(Color.Purple)
                .Header("[yellow]PostgreSQL Database Setup[/]")
                .Padding(1, 0));

        try
        {
            // Build configuration
            var configuration = BuildConfiguration();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                AnsiConsole.Write(
                    new Panel(new Markup("[red]❌ No database connection string found.[/]\n\n" +
                        "[yellow]Please set up user secrets:[/]\n" +
                        "[grey]dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"your-connection-string\"[/]"))
                    .BorderColor(Color.Red)
                    .Padding(1, 0));
                return;
            }

            AnsiConsole.MarkupLine("[green]✅ Database connection string found[/]");
            AnsiConsole.MarkupLine($"[dim]Connection: {MaskConnectionString(connectionString)}[/]");

            // Read the schema SQL file (Azure-compatible version)
            var schemaPath = @"s:\src\rdl\BlazorAI-spec\src\FishingRegs.TestConsole\azure-schema.sql";
            
            if (!File.Exists(schemaPath))
            {
                Console.WriteLine($"❌ Azure schema file not found at: {schemaPath}");
                return;
            }

            var schemaSql = await File.ReadAllTextAsync(schemaPath);
            Console.WriteLine($"📄 Schema file loaded: {schemaSql.Length:N0} characters");

            // Connect to PostgreSQL and execute schema
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            Console.WriteLine("🔗 Connected to PostgreSQL database");
            Console.WriteLine($"Database: {connection.Database}");
            Console.WriteLine($"Server: {connection.Host}:{connection.Port}\n");

            // Execute the schema
            Console.WriteLine("🏗️ Creating database schema...");
            
            using var command = new NpgsqlCommand(schemaSql, connection);
            command.CommandTimeout = 120; // 2 minutes timeout for schema creation
            
            await command.ExecuteNonQueryAsync();
            
            Console.WriteLine("✅ Database schema created successfully!");

            // Verify some key tables exist
            var tablesToCheck = new[] { "states", "counties", "fish_species", "water_bodies", "regulation_documents", "fishing_regulations" };
            
            Console.WriteLine("\n🔍 Verifying tables were created:");
            
            foreach (var tableName in tablesToCheck)
            {
                using var checkCommand = new NpgsqlCommand(
                    "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @tableName);", 
                    connection);
                checkCommand.Parameters.AddWithValue("tableName", tableName);
                
                var exists = (bool)(await checkCommand.ExecuteScalarAsync() ?? false);
                Console.WriteLine($"  {(exists ? "✅" : "❌")} {tableName}");
            }

            Console.WriteLine("\n🎉 Database is ready for use!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask sensitive parts of the connection string for display
        var parts = connectionString.Split(';');
        var maskedParts = new List<string>();
        
        foreach (var part in parts)
        {
            if (part.ToLowerInvariant().Contains("password"))
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    maskedParts.Add($"{keyValue[0]}=***");
                }
                else
                {
                    maskedParts.Add("Password=***");
                }
            }
            else
            {
                maskedParts.Add(part);
            }
        }
        
        return string.Join(";", maskedParts);
    }
}
