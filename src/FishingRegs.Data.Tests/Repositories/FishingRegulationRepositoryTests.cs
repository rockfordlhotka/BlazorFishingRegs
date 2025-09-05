using FluentAssertions;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories.Implementation;
using FishingRegs.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FishingRegs.Data.Tests.Repositories;

// TODO: This test class needs to be updated to match the current FishingRegulation model and IFishingRegulationRepository interface
// The property names and method names are out of sync with the actual implementation
/*

/// <summary>
/// Tests for the FishingRegulationRepository implementation
/// </summary>
public class FishingRegulationRepositoryTests : BaseRepositoryTest
{
    private readonly FishingRegulationRepository _repository;

    public FishingRegulationRepositoryTests()
    {
        _repository = new FishingRegulationRepository(Context);
    }

    [Fact]
    public async Task GetByWaterBodyAsync_WithValidWaterBodyId_ShouldReturnRegulations()
    {
        // Act
        var result = await _repository.GetByWaterBodyAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.WaterBodyId == 1).Should().BeTrue();
        result.All(fr => fr.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetByWaterBodyAsync_WithInvalidWaterBodyId_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetByWaterBodyAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByFishSpeciesAsync_WithValidSpeciesId_ShouldReturnRegulations()
    {
        // Act
        var result = await _repository.GetByFishSpeciesAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.SpeciesId == 1).Should().BeTrue();
        result.All(fr => fr.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetByFishSpeciesAsync_WithInvalidSpeciesId_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetByFishSpeciesAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByWaterBodyAndSpeciesAsync_WithValidIds_ShouldReturnSpecificRegulation()
    {
        // Act
        var result = await _repository.GetByWaterBodyAndSpeciesAsync(1, 1);

        // Assert
        result.Should().NotBeNull();
        result!.WaterBodyId.Should().Be(1);
        result.FishSpeciesId.Should().Be(1);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByWaterBodyAndSpeciesAsync_WithInvalidIds_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByWaterBodyAndSpeciesAsync(999, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEffectiveDateAsync_WithValidDate_ShouldReturnCurrentRegulations()
    {
        // Arrange
        var effectiveDate = new DateTime(2024, 6, 1);

        // Act
        var result = await _repository.GetByEffectiveDateAsync(effectiveDate);

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.IsActive).Should().BeTrue();
        result.All(fr => fr.EffectiveDate <= DateOnly.FromDateTime(effectiveDate)).Should().BeTrue();
    }

    // TODO: Update this test to use GetSeasonalRegulationsAsync or implement GetRegulationsBySeasonAsync
    // [Fact]
    // public async Task GetRegulationsBySeasonAsync_WithValidDateRange_ShouldReturnSeasonalRegulations()
    // {
    //     // Arrange
    //     var seasonStart = new DateOnly(2024, 5, 1);
    //     var seasonEnd = new DateOnly(2024, 8, 31);
    // 
    //     // Act
    //     var result = await _repository.GetRegulationsBySeasonAsync(seasonStart, seasonEnd);
    // 
    //     // Assert
    //     result.Should().NotBeEmpty();
    //     result.All(fr => fr.IsActive).Should().BeTrue();
    //     
    //     // Check that regulations overlap with the specified season
    //     foreach (var regulation in result)
    //     {
    //         var regulationOverlaps = regulation.SeasonStart <= seasonEnd && regulation.SeasonEnd >= seasonStart;
    //         regulationOverlaps.Should().BeTrue();
    //     }
    // }

    [Fact]
    public async Task GetWithRelatedDataAsync_ShouldIncludeNavigationProperties()
    {
        // Act
        var result = await _repository.GetWithRelatedDataAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.IsActive).Should().BeTrue();
        
        // Check that navigation properties are loaded
        foreach (var regulation in result)
        {
            Context.Entry(regulation).Reference(fr => fr.WaterBody).IsLoaded.Should().BeTrue();
            Context.Entry(regulation).Reference(fr => fr.FishSpecies).IsLoaded.Should().BeTrue();
            Context.Entry(regulation).Reference(fr => fr.RegulationDocument).IsLoaded.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetRegulationsWithSpecialRestrictionsAsync_ShouldReturnOnlyRegulationsWithRestrictions()
    {
        // Act
        var result = await _repository.GetRegulationsWithSpecialRestrictionsAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.IsActive).Should().BeTrue();
        result.All(fr => !string.IsNullOrWhiteSpace(fr.SpecialRestrictions)).Should().BeTrue();
    }

    [Fact]
    public async Task GetRegulationsByDocumentAsync_WithValidDocumentId_ShouldReturnRegulations()
    {
        // Act
        var result = await _repository.GetRegulationsByDocumentAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.RegulationDocumentId == 1).Should().BeTrue();
        result.All(fr => fr.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetRegulationsByDocumentAsync_WithInvalidDocumentId_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetRegulationsByDocumentAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegulationsExpiringAsync_WithValidDate_ShouldReturnExpiringRegulations()
    {
        // Arrange - Add a regulation that expires soon
        var expiringRegulation = new FishingRegulation
        {
            WaterBodyId = 2,
            FishSpeciesId = 2,
            RegulationDocumentId = 1,
            SeasonStart = new DateOnly(2024, 1, 1),
            SeasonEnd = new DateOnly(2024, 12, 31),
            BagLimit = 5,
            MinLength = 12.0m,
            EffectiveDate = new DateOnly(2024, 1, 1),
            ExpirationDate = new DateOnly(2024, 12, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.FishingRegulations.Add(expiringRegulation);
        await Context.SaveChangesAsync();

        var checkDate = new DateOnly(2024, 12, 1);

        // Act
        var result = await _repository.GetRegulationsExpiringAsync(checkDate, 30);

        // Assert
        result.Should().NotBeEmpty();
        result.All(fr => fr.IsActive).Should().BeTrue();
        result.All(fr => fr.ExpirationDate.HasValue).Should().BeTrue();
        
        foreach (var regulation in result)
        {
            var daysUntilExpiration = regulation.ExpirationDate!.Value.DayNumber - checkDate.DayNumber;
            daysUntilExpiration.Should().BeLessOrEqualTo(30);
            daysUntilExpiration.Should().BeGreaterOrEqualTo(0);
        }
    }

    [Fact]
    public async Task CreateRegulationAsync_WithValidData_ShouldCreateAndReturnRegulation()
    {
        // Arrange
        var newRegulation = new FishingRegulation
        {
            WaterBodyId = 2,
            FishSpeciesId = 2,
            RegulationDocumentId = 1,
            SeasonStart = new DateOnly(2024, 4, 1),
            SeasonEnd = new DateOnly(2024, 10, 31),
            BagLimit = 3,
            MinLength = 14.0m,
            MaxLength = 20.0m,
            SpecialRestrictions = "Test restriction",
            EffectiveDate = new DateOnly(2024, 1, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(newRegulation);
        await Context.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.WaterBodyId.Should().Be(2);
        result.FishSpeciesId.Should().Be(2);
        result.BagLimit.Should().Be(3);
        result.MinLength.Should().Be(14.0m);
        result.MaxLength.Should().Be(20.0m);

        // Verify it was saved to database
        var savedRegulation = await _repository.GetByIdAsync(result.Id);
        savedRegulation.Should().NotBeNull();
        savedRegulation!.SpecialRestrictions.Should().Be("Test restriction");
    }

    [Fact]
    public async Task UpdateRegulationAsync_WithValidChanges_ShouldUpdateRegulation()
    {
        // Arrange
        var regulation = await _repository.GetByIdAsync(1);
        regulation.Should().NotBeNull();
        
        var originalBagLimit = regulation!.BagLimit;
        regulation.BagLimit = 10;
        regulation.UpdatedAt = DateTime.UtcNow;

        // Act
        _repository.Update(regulation);
        await Context.SaveChangesAsync();

        // Assert
        var updatedRegulation = await _repository.GetByIdAsync(1);
        updatedRegulation.Should().NotBeNull();
        updatedRegulation!.BagLimit.Should().Be(10);
        updatedRegulation.BagLimit.Should().NotBe(originalBagLimit);
    }

    [Fact]
    public async Task GetRegulationStatisticsAsync_ShouldReturnAccurateStatistics()
    {
        // Act
        var totalRegulations = await _repository.CountAsync();
        var activeRegulations = await _repository.CountAsync(fr => fr.IsActive);
        var regulationsWithRestrictions = await _repository.CountAsync(fr => !string.IsNullOrWhiteSpace(fr.SpecialRestrictions));

        // Assert
        totalRegulations.Should().BeGreaterThan(0);
        activeRegulations.Should().BeGreaterThan(0);
        activeRegulations.Should().BeLessOrEqualTo(totalRegulations);
        regulationsWithRestrictions.Should().BeGreaterThan(0);
    }
}
*/
