using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.EntityFrameworkCore.Diagnostics;
using FishingRegs.Data;
using FishingRegs.Data.Models;

namespace FishingRegs.Data.Tests.Infrastructure;

/// <summary>
/// Test database context factory for creating in-memory database instances
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new in-memory database context for testing
    /// </summary>
    /// <param name="databaseName">Unique database name to avoid conflicts between tests</param>
    /// <returns>Configured FishingRegsDbContext for testing</returns>
    public static FishingRegsDbContext CreateInMemoryContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<FishingRegsDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new FishingRegsDbContext(options);
        
        // Ensure the database is created
        context.Database.EnsureCreated();
        
        // Seed test data
        SeedTestData(context);
        
        return context;
    }

    /// <summary>
    /// Seeds the database with test data
    /// </summary>
    /// <param name="context">The database context to seed</param>
    public static void SeedTestData(FishingRegsDbContext context)
    {
        // Note: The DbContext already seeds States (1-6) and FishSpecies (1-10) via HasData()
        // We only need to add Counties and WaterBodies that reference the existing seeded data
        
        // Add test counties (using existing State IDs from DbContext seed data)
        var cookCounty = new County
        {
            Id = 1,
            Name = "Cook County",
            StateId = 1, // Minnesota (already seeded via HasData)
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var stLouisCounty = new County
        {
            Id = 2,
            Name = "St. Louis County",
            StateId = 1, // Minnesota (already seeded via HasData)
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Counties.AddRange(cookCounty, stLouisCounty);

        // Add test water bodies (using existing State and new County IDs)
        var lakeSuperior = new WaterBody
        {
            Id = 1,
            Name = "Lake Superior",
            StateId = 1, // Minnesota (already seeded via HasData)
            CountyId = 1,
            WaterType = "lake",
            DnrId = "LS001",
            Latitude = 47.7511m,
            Longitude = -91.0591m,
            SurfaceAreaAcres = 20000000m,
            MaxDepthFeet = 1332,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var mileLacs = new WaterBody
        {
            Id = 2,
            Name = "Mille Lacs Lake",
            StateId = 1, // Minnesota (already seeded via HasData)
            CountyId = 2,
            WaterType = "lake",
            DnrId = "ML001",
            Latitude = 46.2067m,
            Longitude = -93.6628m,
            SurfaceAreaAcres = 132516m,
            MaxDepthFeet = 42,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.WaterBodies.AddRange(lakeSuperior, mileLacs);

        // Add test regulation documents
        var mnRegDoc = new RegulationDocument
        {
            Id = Guid.NewGuid(),
            FileName = "mn-fishing-regs-2024.pdf",
            OriginalFileName = "Minnesota Fishing Regulations 2024.pdf",
            FileSizeBytes = 1024000,
            MimeType = "application/pdf",
            BlobStorageUrl = "https://test.blob.core.windows.net/regulations/mn-fishing-regs-2024.pdf",
            BlobContainer = "regulations",
            StateId = 1, // Minnesota (already seeded via HasData)
            RegulationYear = 2024,
            ProcessingStatus = "completed",
            CreatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.RegulationDocuments.Add(mnRegDoc);

        // Add test fishing regulations (using existing State and FishSpecies IDs)
        var superiorWalleyeReg = new FishingRegulation
        {
            // Id will be auto-generated since it's marked as Identity
            WaterBodyId = 1,
            SpeciesId = 1, // Walleye (already seeded via HasData)
            RegulationType = "general",
            RegulationYear = 2024,
            SourceDocumentId = mnRegDoc.Id,
            EffectiveDate = new DateOnly(2024, 1, 1),
            SeasonStartMonth = 5,
            SeasonStartDay = 15,
            SeasonEndMonth = 2,
            SeasonEndDay = 28,
            DailyLimit = 6,
            MinimumSizeInches = 15.0m,
            SpecialRegulations = new List<string> { "No night fishing during spawning season" },
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.FishingRegulations.Add(superiorWalleyeReg);

        context.SaveChanges();
    }
}
