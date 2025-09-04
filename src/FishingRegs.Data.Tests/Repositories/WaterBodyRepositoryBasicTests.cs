using FluentAssertions;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories.Implementation;
using FishingRegs.Data.Tests.Infrastructure;
using Xunit;

namespace FishingRegs.Data.Tests.Repositories;

/// <summary>
/// Basic tests for WaterBodyRepository
/// </summary>
public class WaterBodyRepositoryBasicTests : BaseRepositoryTest
{
    private readonly WaterBodyRepository _repository;

    public WaterBodyRepositoryBasicTests()
    {
        _repository = new WaterBodyRepository(Context);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnWaterBody()
    {
        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Lake Superior");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOnlyActiveWaterBodies()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
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
    public async Task SearchByNameAsync_WithPartialMatch_ShouldReturnMatchingWaterBodies()
    {
        // Act
        var result = await _repository.SearchByNameAsync("Lake");

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task GetByGeographicAreaAsync_WithValidBounds_ShouldReturnWaterBodiesInArea()
    {
        // Arrange - Create bounds that include Lake Superior
        var minLat = 47.0m;
        var maxLat = 48.0m;
        var minLon = -92.0m;
        var maxLon = -90.0m;

        // Act
        var result = await _repository.GetByGeographicAreaAsync(minLat, maxLat, minLon, maxLon);

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.All(wb => wb.Latitude >= minLat && wb.Latitude <= maxLat).Should().BeTrue();
        result.All(wb => wb.Longitude >= minLon && wb.Longitude <= maxLon).Should().BeTrue();
    }

    [Fact]
    public async Task GetWithRelatedDataAsync_ShouldIncludeRelatedEntities()
    {
        // Act
        var result = await _repository.GetWithRelatedDataAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.IsActive).Should().BeTrue();
        result.Should().BeInAscendingOrder(wb => wb.State.Name).And.BeInAscendingOrder(wb => wb.Name);
    }

    [Fact]
    public async Task GetByIdWithRelatedDataAsync_WithValidId_ShouldReturnWaterBodyWithRelatedData()
    {
        // Act
        var result = await _repository.GetByIdWithRelatedDataAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.State.Should().NotBeNull();
        result.County.Should().NotBeNull();
    }

    [Fact]
    public async Task AddAsync_WithValidWaterBody_ShouldAddAndReturnWaterBody()
    {
        // Arrange
        var newWaterBody = new WaterBody
        {
            Name = "Test Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(newWaterBody);
        await Context.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Test Lake");

        // Verify it was saved
        var savedEntity = await _repository.GetByIdAsync(result.Id);
        savedEntity.Should().NotBeNull();
        savedEntity!.Name.Should().Be("Test Lake");
    }
}
