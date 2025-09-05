using FishingRegs.TestConsole;
using Spectre.Console;

// Display the application header
AnsiConsole.Write(
    new FigletText("FishingRegs")
        .LeftJustified()
        .Color(Color.Blue));

AnsiConsole.Write(
    new Panel(new Text("Test Console Application", style: "bold"))
        .BorderColor(Color.Blue)
        .Padding(1, 0));

// Create the menu options
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose a [green]test option[/]:")
        .PageSize(10)
        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
        .AddChoices(new[] {
            "Simple AI Extraction Test",
            "Full Database Population Test", 
            "Create Database Schema",
            "Exit"
        }));

// Handle the selection
switch (choice)
{
    case "Simple AI Extraction Test":
        AnsiConsole.MarkupLine("[green]Running Simple AI Extraction Test...[/]");
        await SimpleAiExtractionTest.RunAiExtractionTest(args);
        break;
        
    case "Full Database Population Test":
        AnsiConsole.MarkupLine("[green]Running Full Database Population Test...[/]");
        await DatabasePopulationTestProgram.MainDatabase(args);
        break;
        
    case "Create Database Schema":
        AnsiConsole.MarkupLine("[green]Creating Database Schema...[/]");
        await DatabaseSchemaCreator.CreateSchema(args);
        break;
        
    case "Exit":
        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
        return;
        
    default:
        AnsiConsole.MarkupLine("[red]Invalid choice.[/]");
        break;
}
