using FishingRegs.TestConsole;

namespace FishingRegs.TestConsole;

public static class TestMockData
{
    public static async Task RunTestAsync()
    {
        // Test all mock data loading methods
        Console.WriteLine("=== Testing Mock Database Population ===");

        // Test 1: Generate mock data
        Console.WriteLine("\n1. Testing mock data generation:");
        try 
        {
            await MockDatabasePopulationTest.RunMockDatabaseTest(new[] { "--mock" });
            Console.WriteLine("✅ Mock data generation test passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Mock data generation test failed: {ex.Message}");
        }

        Console.WriteLine("\n=== All tests completed ===");
    }
}
