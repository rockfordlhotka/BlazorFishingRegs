using FishingRegs.TestConsole;

Console.WriteLine("FishingRegs Test Console");
Console.WriteLine("=======================");
Console.WriteLine("1. Simple AI Extraction Test (no database)");
Console.WriteLine("2. Full Database Population Test");
Console.WriteLine();
Console.Write("Choose test (1 or 2): ");

var choice = Console.ReadLine();

if (choice == "1")
{
    // Run the simple AI extraction test
    await SimpleAiExtractionTest.RunAiExtractionTest(args);
}
else
{
    // Run the full database population test
    await DatabasePopulationTestProgram.MainDatabase(args);
}
