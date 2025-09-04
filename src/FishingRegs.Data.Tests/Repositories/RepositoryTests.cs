using FluentAssertions;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories.Implementation;
using FishingRegs.Data.Tests.Infrastructure;
using Xunit;

namespace FishingRegs.Data.Tests.Repositories;

/// <summary>
/// Tests for the base Repository<T> implementation
/// </summary>
public class RepositoryTests : BaseRepositoryTest
{
    private readonly TestWaterBodyRepository _repository;

    public RepositoryTests()
    {
        _repository = new TestWaterBodyRepository(Context);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnEntity()
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
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FindAsync_WithValidPredicate_ShouldReturnMatchingEntities()
    {
        // Act
        var result = await _repository.FindAsync(wb => wb.StateId == 1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(wb => wb.StateId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_WithNoMatches_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _repository.FindAsync(wb => wb.StateId == 999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithValidPredicate_ShouldReturnFirstMatch()
    {
        // Act
        var result = await _repository.FirstOrDefaultAsync(wb => wb.StateId == 1);

        // Assert
        result.Should().NotBeNull();
        result!.StateId.Should().Be(1);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatches_ShouldReturnNull()
    {
        // Act
        var result = await _repository.FirstOrDefaultAsync(wb => wb.StateId == 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldAddAndReturnEntity()
    {
        // Arrange
        var newWaterBody = new WaterBody
        {
            Name = "Test Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(newWaterBody);
        await Context.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Test Lake");

        // Verify it was saved to database
        var savedEntity = await _repository.GetByIdAsync(result.Id);
        savedEntity.Should().NotBeNull();
        savedEntity!.Name.Should().Be("Test Lake");
    }

    [Fact]
    public async Task AddRangeAsync_WithValidEntities_ShouldAddAllEntities()
    {
        // Arrange
        var newWaterBodies = new[]
        {
            new WaterBody
            {
                Name = "Test Lake 1",
                StateId = 1,
                WaterType = "lake",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new WaterBody
            {
                Name = "Test Lake 2",
                StateId = 2,
                WaterType = "lake",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var originalCount = (await _repository.GetAllAsync()).Count();

        // Act
        await _repository.AddRangeAsync(newWaterBodies);
        await Context.SaveChangesAsync();

        // Assert
        var newCount = (await _repository.GetAllAsync()).Count();
        newCount.Should().Be(originalCount + 2);
    }

    [Fact]
    public void Update_WithValidEntity_ShouldMarkEntityAsModified()
    {
        // Arrange
        var waterBody = Context.WaterBodies.First();
        var originalName = waterBody.Name;
        waterBody.Name = "Updated Lake Name";

        // Act
        _repository.Update(waterBody);

        // Assert
        Context.Entry(waterBody).State.Should().Be(Microsoft.EntityFrameworkCore.EntityState.Modified);
        waterBody.Name.Should().Be("Updated Lake Name");
        waterBody.Name.Should().NotBe(originalName);
    }

    [Fact]
    public void Remove_WithValidEntity_ShouldMarkEntityAsDeleted()
    {
        // Arrange
        var waterBody = Context.WaterBodies.First();

        // Act
        _repository.Remove(waterBody);

        // Assert
        Context.Entry(waterBody).State.Should().Be(Microsoft.EntityFrameworkCore.EntityState.Deleted);
    }

    [Fact]
    public void RemoveRange_WithValidEntities_ShouldMarkAllEntitiesAsDeleted()
    {
        // Arrange
        var waterBodies = Context.WaterBodies.Take(2).ToList();

        // Act
        _repository.RemoveRange(waterBodies);

        // Assert
        foreach (var waterBody in waterBodies)
        {
            Context.Entry(waterBody).State.Should().Be(Microsoft.EntityFrameworkCore.EntityState.Deleted);
        }
    }

    [Fact]
    public async Task CountAsync_WithoutPredicate_ShouldReturnTotalCount()
    {
        // Act
        var count = await _repository.CountAsync();

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CountAsync_WithPredicate_ShouldReturnFilteredCount()
    {
        // Act
        var count = await _repository.CountAsync(wb => wb.StateId == 1);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnyAsync_WithMatchingPredicate_ShouldReturnTrue()
    {
        // Act
        var exists = await _repository.AnyAsync(wb => wb.StateId == 1);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithNonMatchingPredicate_ShouldReturnFalse()
    {
        // Act
        var exists = await _repository.AnyAsync(wb => wb.StateId == 999);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void AddAsync_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await _repository.AddAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Update_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => _repository.Update(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Remove_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => _repository.Remove(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveRange_WithNullEntities_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => _repository.RemoveRange(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

/// <summary>
/// Concrete implementation of Repository for testing purposes
/// </summary>
internal class TestWaterBodyRepository : Repository<WaterBody>
{
    public TestWaterBodyRepository(FishingRegsDbContext context) : base(context)
    {
    }
}
