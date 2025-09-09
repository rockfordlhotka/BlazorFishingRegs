using FishingRegs.TestConsole;
using Spectre.Console;

// ========================================
// Simplified FishingRegs Test Console
// ========================================
// This console application provides essential testing capabilities:
// 1. Comprehensive Data Ingestion - NEW! AI extracts ALL data systematically
// 2. Streaming Data Ingestion - Real-time AI extraction and database population
// 3. Mock Data Population Test - Database population testing without AI calls
// 4. Database Schema Creation - Setup database structure
// 5. Clear Database - Remove all data from database tables
// 6. Water Body Extraction Diagnostic - DEBUG: Analyze extraction issues
// ========================================

// Display the application header
AnsiConsole.Write(
    new FigletText("FishingRegs")
        .LeftJustified()
        .Color(Color.Blue));

AnsiConsole.Write(
    new Panel(new Text("Test Console Application - Enhanced with Comprehensive AI", style: "bold"))
        .BorderColor(Color.Blue)
        .Padding(1, 0));

// Create the enhanced menu options
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose a [green]test option[/]:")
        .PageSize(10)
        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
        .AddChoices(new[] {
            "🎯 Regex Water Body Extraction (SIMPLE & FAST)",
            "🏗️ Regex Database Population (SIMPLE & RELIABLE)",
            "🚨 Regex REAL Database Population (PRODUCTION!)",
            "🔥 Comprehensive Data Ingestion (NEW!)",
            "Streaming Data Ingestion (Real-time)",
            "Mock Data Population Test (No AI)",
            "Create Database Schema",
            "Clear Database",
            "🔍 Water Body Extraction Diagnostic (DEBUG)",
            "Exit"
        }));

// Handle the selection
switch (choice)
{
    case "🎯 Regex Water Body Extraction (SIMPLE & FAST)":
        AnsiConsole.MarkupLine("[green]Running Regex Water Body Extraction...[/]");
        await RegexWaterBodyExtraction.RunRegexExtraction(args);
        break;

    case "🏗️ Regex Database Population (SIMPLE & RELIABLE)":
        AnsiConsole.MarkupLine("[green]Running Regex Database Population (In-Memory Test)...[/]");
        await RegexDatabasePopulation.RunRegexDatabasePopulation(args);
        break;

    case "🚨 Regex REAL Database Population (PRODUCTION!)":
        AnsiConsole.MarkupLine("[red]Running Regex REAL Database Population...[/]");
        await RegexRealDatabasePopulation.RunRegexRealDatabasePopulation(args);
        break;
        
    case "🔥 Comprehensive Data Ingestion (NEW!)":
        AnsiConsole.MarkupLine("[green]Running Comprehensive Data Ingestion...[/]");
        await ComprehensiveDataIngestionProgram.RunComprehensiveIngestion(args);
        break;
        
    case "Streaming Data Ingestion (Real-time)":
        AnsiConsole.MarkupLine("[green]Running Streaming Data Ingestion...[/]");
        await DatabasePopulationTestProgram.MainDatabaseStream(args);
        break;
        
    case "Mock Data Population Test (No AI)":
        AnsiConsole.MarkupLine("[green]Running Mock Data Population Test...[/]");
        await MockDatabasePopulationTest.RunMockDatabaseTest(args);
        break;
        
    case "Create Database Schema":
        AnsiConsole.MarkupLine("[green]Creating Database Schema...[/]");
        await DatabaseSchemaCreator.CreateSchema(args);
        break;
        
    case "Clear Database":
        AnsiConsole.MarkupLine("[yellow]Clearing Database...[/]");
        await DatabaseCleaner.ClearDatabase(args);
        break;
        
    case "🔍 Water Body Extraction Diagnostic (DEBUG)":
        AnsiConsole.MarkupLine("[yellow]Running Water Body Extraction Diagnostic...[/]");
        await WaterBodyExtractionDiagnostic.RunDiagnostic(args);
        break;
        
    case "Exit":
        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
        return;
        
    default:
        AnsiConsole.MarkupLine("[red]Invalid choice.[/]");
        break;
}
