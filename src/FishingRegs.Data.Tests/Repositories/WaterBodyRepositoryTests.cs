using FluentAssertions;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories.Implementation;
using FishingRegs.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FishingRegs.Data.Tests.Repositories;

/// <summary>
/// Tests for the WaterBodyRepository implementation
/// </summary>
public class WaterBodyRepositoryTests : BaseRepositoryTest
{
    private readonly WaterBodyRepository _repository;

    public WaterBodyRepositoryTests()
    {
        _repository = new WaterBodyRepository(Context);
    }

    [Fact]
    public async Task GetByStateAsync_WithValidStateId_ShouldReturnWaterBodiesInState()
    {
        // Act
        var result = await _repository.GetByStateAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.StateId == 1).Should().BeTrue();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task GetByStateAsync_WithInvalidStateId_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetByStateAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByCountyAsync_WithValidCountyId_ShouldReturnWaterBodiesInCounty()
    {
        // Act
        var result = await _repository.GetByCountyAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.CountyId == 1).Should().BeTrue();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task GetByCountyAsync_WithInvalidCountyId_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetByCountyAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByTypeAsync_WithValidWaterType_ShouldReturnWaterBodiesOfType()
    {
        // Act
        var result = await _repository.GetByTypeAsync("lake");

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.WaterType == "lake").Should().BeTrue();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task GetByTypeAsync_WithInvalidWaterType_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.GetByTypeAsync("nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByNameAsync_WithPartialMatch_ShouldReturnMatchingWaterBodies()
    {
        // Act
        var result = await _repository.SearchByNameAsync("Lake");

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.Name.Contains("Lake", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task SearchByNameAsync_WithNoMatches_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.SearchByNameAsync("NonexistentLake");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByNameAsync_CaseInsensitive_ShouldReturnMatchingWaterBodies()
    {
        // Act
        var result = await _repository.SearchByNameAsync("superior");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(wb => wb.Name.Contains("Superior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetByGeographicAreaAsync_WithValidBounds_ShouldReturnWaterBodiesInArea()
    {
        // Arrange - Create bounds that include Lake Superior
        var bounds = new GeographicBounds
        {
            NorthLatitude = 48.0m,
            SouthLatitude = 47.0m,
            EastLongitude = -90.0m,
            WestLongitude = -92.0m
        };

        // Act
        var result = await _repository.GetByGeographicAreaAsync(
            bounds.SouthLatitude, 
            bounds.NorthLatitude, 
            bounds.WestLongitude, 
            bounds.EastLongitude);

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.All(wb => wb.Latitude >= bounds.SouthLatitude && wb.Latitude <= bounds.NorthLatitude).Should().BeTrue();
        result.All(wb => wb.Longitude >= bounds.WestLongitude && wb.Longitude <= bounds.EastLongitude).Should().BeTrue();
    }

    [Fact]
    public async Task GetByGeographicAreaAsync_WithBoundsContainingNoWaterBodies_ShouldReturnEmptyCollection()
    {
        // Arrange - Create bounds that don't include any water bodies
        var bounds = new GeographicBounds
        {
            NorthLatitude = 30.0m,
            SouthLatitude = 29.0m,
            EastLongitude = -80.0m,
            WestLongitude = -81.0m
        };

        // Act
        var result = await _repository.GetByGeographicAreaAsync(
            bounds.SouthLatitude, 
            bounds.NorthLatitude, 
            bounds.WestLongitude, 
            bounds.EastLongitude);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWithRelatedDataAsync_ShouldIncludeRelatedEntities()
    {
        // Act
        var result = await _repository.GetWithRelatedDataAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        
        // Check that navigation properties are loaded
        foreach (var waterBody in result)
        {
            if (waterBody.StateId > 0)
            {
                Context.Entry(waterBody).Reference(wb => wb.State).IsLoaded.Should().BeTrue();
            }
            if (waterBody.CountyId.HasValue)
            {
                Context.Entry(waterBody).Reference(wb => wb.County).IsLoaded.Should().BeTrue();
            }
            Context.Entry(waterBody).Collection(wb => wb.FishingRegulations).IsLoaded.Should().BeTrue();
        }
    }

    // TODO: Implement GetByDnrIdAsync method in repository
    // [Fact]
    // public async Task GetByDnrIdAsync_WithValidDnrId_ShouldReturnWaterBody()
    // {
    //     // Act
    //     var result = await _repository.GetByDnrIdAsync("LS001");

    //     // Assert
    //     result.Should().NotBeNull();
    //     result!.DnrId.Should().Be("LS001");
    //     result.Name.Should().Be("Lake Superior");
    // }

    // TODO: Implement GetByDnrIdAsync method in repository
    // [Fact]
    // public async Task GetByDnrIdAsync_WithInvalidDnrId_ShouldReturnNull()
    // {
    //     // Act
    //     var result = await _repository.GetByDnrIdAsync("INVALID");

    //     // Assert
    //     result.Should().BeNull();
    // }

    // TODO: Implement GetLargestWaterBodiesAsync method in repository
    // [Fact]
    // public async Task GetLargestWaterBodiesAsync_ShouldReturnWaterBodiesOrderedBySize()
    // {
    //     // Act
    //     var result = await _repository.GetLargestWaterBodiesAsync(10);

    //     // Assert
    //     result.Should().NotBeEmpty();
    //     result.Should().BeInDescendingOrder(wb => wb.SurfaceAreaAcres);
    //     result.Count().Should().BeLessOrEqualTo(10);
    //     result.All(wb => wb.IsActive).Should().BeTrue();
    //     result.All(wb => wb.SurfaceAreaAcres.HasValue).Should().BeTrue();
    // }

    // TODO: Implement GetWaterBodiesWithRegulationsAsync method in repository
    // [Fact]
    // public async Task GetWaterBodiesWithRegulationsAsync_ShouldReturnOnlyWaterBodiesWithRegulations()
    // {
    //     // Act
    //     var result = await _repository.GetWaterBodiesWithRegulationsAsync();

    //     // Assert
    //     result.Should().NotBeEmpty();
    //     result.All(wb => wb.IsActive).Should().BeTrue();
        
    //     // Verify each water body has at least one regulation
    //     foreach (var waterBody in result)
    //     {
    //         var regulationCount = await Context.FishingRegulations
    //             .CountAsync(fr => fr.WaterBodyId == waterBody.Id && fr.IsActive);
    //         regulationCount.Should().BeGreaterThan(0);
    //     }
    // }

    [Fact] 
    public async Task GetActiveWaterBodiesAsync_ShouldReturnOnlyActiveWaterBodies()
    {
        // Arrange - Add an inactive water body
        var inactiveWaterBody = new WaterBody
        {
            Name = "Inactive Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Context.WaterBodies.Add(inactiveWaterBody);
        await Context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert - The base GetAllAsync should only return active water bodies
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().NotContain(wb => wb.Name == "Inactive Lake");
    }
}

/// <summary>
/// Helper class for geographic bounds testing
/// </summary>
public class GeographicBounds
{
    public decimal NorthLatitude { get; set; }
    public decimal SouthLatitude { get; set; }
    public decimal EastLongitude { get; set; }
    public decimal WestLongitude { get; set; }
}
