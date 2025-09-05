using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using FishingRegs.Data.Models;
using System.Text.Json;

namespace FishingRegs.Data;

/// <summary>
/// Entity Framework DbContext for the Fishing Regulations application
/// </summary>
public class FishingRegsDbContext : DbContext
{
    public FishingRegsDbContext(DbContextOptions<FishingRegsDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<State> States { get; set; }
    public DbSet<County> Counties { get; set; }
    public DbSet<FishSpecies> FishSpecies { get; set; }
    public DbSet<WaterBody> WaterBodies { get; set; }
    public DbSet<WaterBodySpecies> WaterBodySpecies { get; set; }

    // Regulation entities
    public DbSet<RegulationDocument> RegulationDocuments { get; set; }
    public DbSet<FishingRegulation> FishingRegulations { get; set; }

    // User entities
    public DbSet<User> Users { get; set; }
    public DbSet<UserFavorite> UserFavorites { get; set; }

    // Analytics entities
    public DbSet<SearchHistory> SearchHistory { get; set; }
    public DbSet<RegulationAuditLog> RegulationAuditLogs { get; set; }

    // Views
    public DbSet<LakeSummaryView> LakeSummaryViews { get; set; }
    public DbSet<CurrentFishingRegulationsView> CurrentFishingRegulationsViews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure PostgreSQL-specific features
        ConfigurePostgreSQLFeatures(modelBuilder);

        // Configure entity relationships
        ConfigureRelationships(modelBuilder);

        // Configure value conversions
        ConfigureValueConversions(modelBuilder);

        // Configure indexes
        ConfigureIndexes(modelBuilder);

        // Configure views
        ConfigureViews(modelBuilder);

        // Seed initial data
        SeedInitialData(modelBuilder);
    }

    private void ConfigurePostgreSQLFeatures(ModelBuilder modelBuilder)
    {
        // Create value comparer for List<string> collections
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // Configure array types for PostgreSQL with value comparers
        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.SpecialRegulations)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.RequiredStamps)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        modelBuilder.Entity<RegulationAuditLog>()
            .Property(e => e.ChangedFields)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.SpecialRegulations)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.RequiredStamps)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        // Configure JSONB for RegulationDocument ExtractedDataJson
        modelBuilder.Entity<RegulationDocument>()
            .Property(e => e.ExtractedDataJson)
            .HasColumnType("jsonb");

        // Configure precision for decimal types
        modelBuilder.Entity<WaterBody>()
            .Property(e => e.Latitude)
            .HasPrecision(10, 8);

        modelBuilder.Entity<WaterBody>()
            .Property(e => e.Longitude)
            .HasPrecision(11, 8);

        modelBuilder.Entity<WaterBody>()
            .Property(e => e.SurfaceAreaAcres)
            .HasPrecision(10, 2);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.MinimumSizeInches)
            .HasPrecision(5, 2);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.MaximumSizeInches)
            .HasPrecision(5, 2);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.ProtectedSlotMinInches)
            .HasPrecision(5, 2);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.ProtectedSlotMaxInches)
            .HasPrecision(5, 2);

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.ConfidenceScore)
            .HasPrecision(5, 4);

        modelBuilder.Entity<RegulationDocument>()
            .Property(e => e.ConfidenceScore)
            .HasPrecision(5, 4);
    }

    private void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // State to Counties (one-to-many)
        modelBuilder.Entity<County>()
            .HasOne(c => c.State)
            .WithMany(s => s.Counties)
            .HasForeignKey(c => c.StateId)
            .OnDelete(DeleteBehavior.Cascade);

        // State to WaterBodies (one-to-many)
        modelBuilder.Entity<WaterBody>()
            .HasOne(wb => wb.State)
            .WithMany(s => s.WaterBodies)
            .HasForeignKey(wb => wb.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        // County to WaterBodies (one-to-many)
        modelBuilder.Entity<WaterBody>()
            .HasOne(wb => wb.County)
            .WithMany(c => c.WaterBodies)
            .HasForeignKey(wb => wb.CountyId)
            .OnDelete(DeleteBehavior.SetNull);

        // WaterBodySpecies junction table
        modelBuilder.Entity<WaterBodySpecies>()
            .HasKey(wbs => wbs.Id);

        modelBuilder.Entity<WaterBodySpecies>()
            .HasIndex(wbs => new { wbs.WaterBodyId, wbs.SpeciesId })
            .IsUnique();

        // FishingRegulation relationships
        modelBuilder.Entity<FishingRegulation>()
            .HasOne(fr => fr.WaterBody)
            .WithMany(wb => wb.FishingRegulations)
            .HasForeignKey(fr => fr.WaterBodyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FishingRegulation>()
            .HasOne(fr => fr.Species)
            .WithMany(fs => fs.FishingRegulations)
            .HasForeignKey(fr => fr.SpeciesId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FishingRegulation>()
            .HasOne(fr => fr.SourceDocument)
            .WithMany(rd => rd.FishingRegulations)
            .HasForeignKey(fr => fr.SourceDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        // User relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.PreferredState)
            .WithMany(s => s.PreferredByUsers)
            .HasForeignKey(u => u.PreferredStateId)
            .OnDelete(DeleteBehavior.SetNull);

        // UserFavorites junction table
        modelBuilder.Entity<UserFavorite>()
            .HasIndex(uf => new { uf.UserId, uf.WaterBodyId })
            .IsUnique();

        // Audit log relationships
        modelBuilder.Entity<RegulationAuditLog>()
            .HasOne(ral => ral.Regulation)
            .WithMany()
            .HasForeignKey(ral => ral.RegulationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RegulationAuditLog>()
            .HasOne(ral => ral.ChangedByUser)
            .WithMany(u => u.AuditLogEntries)
            .HasForeignKey(ral => ral.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureValueConversions(ModelBuilder modelBuilder)
    {
        // Configure DateOnly conversions for PostgreSQL
        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.EffectiveDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.ExpirationDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.SeasonOpenDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<FishingRegulation>()
            .Property(e => e.SeasonCloseDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        // Similar conversions for view models
        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.EffectiveDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.ExpirationDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.SeasonOpenDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .Property(e => e.SeasonCloseDate)
            .HasConversion<DateOnlyConverter, DateOnlyComparer>();
    }

    private void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Water bodies indexes
        modelBuilder.Entity<WaterBody>()
            .HasIndex(wb => new { wb.StateId, wb.CountyId })
            .HasDatabaseName("idx_water_bodies_state_county");

        modelBuilder.Entity<WaterBody>()
            .HasIndex(wb => wb.WaterType)
            .HasDatabaseName("idx_water_bodies_type");

        // Fishing regulations indexes
        modelBuilder.Entity<FishingRegulation>()
            .HasIndex(fr => fr.WaterBodyId)
            .HasDatabaseName("idx_fishing_regulations_water_body");

        modelBuilder.Entity<FishingRegulation>()
            .HasIndex(fr => fr.SpeciesId)
            .HasDatabaseName("idx_fishing_regulations_species");

        modelBuilder.Entity<FishingRegulation>()
            .HasIndex(fr => fr.RegulationYear)
            .HasDatabaseName("idx_fishing_regulations_year");

        modelBuilder.Entity<FishingRegulation>()
            .HasIndex(fr => fr.IsActive)
            .HasFilter("is_active = true")
            .HasDatabaseName("idx_fishing_regulations_active");

        // Users indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("idx_users_email");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.AzureAdObjectId)
            .IsUnique()
            .HasDatabaseName("idx_users_azure_id");
    }

    private void ConfigureViews(ModelBuilder modelBuilder)
    {
        // Configure views as keyless entities since they're read-only
        modelBuilder.Entity<LakeSummaryView>()
            .ToView("lake_summary");

        modelBuilder.Entity<CurrentFishingRegulationsView>()
            .ToView("current_fishing_regulations");
    }

    private void SeedInitialData(ModelBuilder modelBuilder)
    {
        // Seed states
        var states = new[]
        {
            new State { Id = 1, Code = "MN", Name = "Minnesota" },
            new State { Id = 2, Code = "WI", Name = "Wisconsin" },
            new State { Id = 3, Code = "MI", Name = "Michigan" },
            new State { Id = 4, Code = "IA", Name = "Iowa" },
            new State { Id = 5, Code = "ND", Name = "North Dakota" },
            new State { Id = 6, Code = "SD", Name = "South Dakota" }
        };

        modelBuilder.Entity<State>().HasData(states);

        // Seed fish species
        var fishSpecies = new[]
        {
            new FishSpecies { Id = 1, CommonName = "Walleye", ScientificName = "Sander vitreus", SpeciesCode = "WAE" },
            new FishSpecies { Id = 2, CommonName = "Northern Pike", ScientificName = "Esox lucius", SpeciesCode = "NOP" },
            new FishSpecies { Id = 3, CommonName = "Largemouth Bass", ScientificName = "Micropterus salmoides", SpeciesCode = "LMB" },
            new FishSpecies { Id = 4, CommonName = "Smallmouth Bass", ScientificName = "Micropterus dolomieu", SpeciesCode = "SMB" },
            new FishSpecies { Id = 5, CommonName = "Lake Trout", ScientificName = "Salvelinus namaycush", SpeciesCode = "LAT" },
            new FishSpecies { Id = 6, CommonName = "Muskie", ScientificName = "Esox masquinongy", SpeciesCode = "MUE" },
            new FishSpecies { Id = 7, CommonName = "Yellow Perch", ScientificName = "Perca flavescens", SpeciesCode = "YEP" },
            new FishSpecies { Id = 8, CommonName = "Bluegill", ScientificName = "Lepomis macrochirus", SpeciesCode = "BLG" },
            new FishSpecies { Id = 9, CommonName = "Crappie", ScientificName = "Pomoxis nigromaculatus", SpeciesCode = "CRP" },
            new FishSpecies { Id = 10, CommonName = "Salmon", ScientificName = "Salmo salar", SpeciesCode = "SAL" }
        };

        modelBuilder.Entity<FishSpecies>().HasData(fishSpecies);
    }
}

/// <summary>
/// DateOnly converter for PostgreSQL compatibility
/// </summary>
public class DateOnlyConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, DateTime>
{
    public DateOnlyConverter() : base(
        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
        dateTime => DateOnly.FromDateTime(dateTime))
    {
    }
}

/// <summary>
/// DateOnly comparer for Entity Framework
/// </summary>
public class DateOnlyComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<DateOnly>
{
    public DateOnlyComparer() : base(
        (x, y) => x == y,
        dateOnly => dateOnly.GetHashCode())
    {
    }
}
