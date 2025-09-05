using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories;
using FishingRegs.Data.Tests.Infrastructure;
using Xunit;

namespace FishingRegs.Data.Tests;

/// <summary>
/// Tests for the UnitOfWork implementation
/// </summary>
public class UnitOfWorkTests : BaseRepositoryTest
{
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        _unitOfWork = new UnitOfWork(Context);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _unitOfWork?.Dispose();
        }
        base.Dispose(disposing);
    }

    [Fact]
    public void WaterBodies_ShouldReturnWaterBodyRepository()
    {
        // Act
        var repository = _unitOfWork.WaterBodies;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IWaterBodyRepository>();
    }

    [Fact]
    public void FishingRegulations_ShouldReturnFishingRegulationRepository()
    {
        // Act
        var repository = _unitOfWork.FishingRegulations;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IFishingRegulationRepository>();
    }

    [Fact]
    public void RegulationDocuments_ShouldReturnRegulationDocumentRepository()
    {
        // Act
        var repository = _unitOfWork.RegulationDocuments;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IRegulationDocumentRepository>();
    }

    [Fact]
    public void States_ShouldReturnStateRepository()
    {
        // Act
        var repository = _unitOfWork.States;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IStateRepository>();
    }

    [Fact]
    public void Counties_ShouldReturnCountyRepository()
    {
        // Act
        var repository = _unitOfWork.Counties;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<ICountyRepository>();
    }

    [Fact]
    public void FishSpecies_ShouldReturnFishSpeciesRepository()
    {
        // Act
        var repository = _unitOfWork.FishSpecies;

        // Assert
        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IFishSpeciesRepository>();
    }

    [Fact]
    public void Repositories_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Act
        var waterBodies1 = _unitOfWork.WaterBodies;
        var waterBodies2 = _unitOfWork.WaterBodies;

        var fishingRegulations1 = _unitOfWork.FishingRegulations;
        var fishingRegulations2 = _unitOfWork.FishingRegulations;

        // Assert
        waterBodies1.Should().BeSameAs(waterBodies2);
        fishingRegulations1.Should().BeSameAs(fishingRegulations2);
    }

    [Fact]
    public async Task SaveChangesAsync_WithValidChanges_ShouldReturnNumberOfAffectedEntities()
    {
        // Arrange
        var newWaterBody = new WaterBody
        {
            Name = "Test Lake for UoW",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.WaterBodies.AddAsync(newWaterBody);

        // Act
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().BeGreaterThan(0);
        newWaterBody.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutChanges_ShouldReturnZero()
    {
        // Act
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CoordinatedOperations_ShouldWorkAcrossMultipleRepositories()
    {
        // Arrange - Create related entities across multiple repositories
        var newState = new State
        {
            Name = "Test State",
            Code = "TS",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var newCounty = new County
        {
            Name = "Test County",
            StateId = 1, // Use existing state
            CreatedAt = DateTimeOffset.UtcNow
        };

        var newWaterBody = new WaterBody
        {
            Name = "Test Coordinated Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Add entities through different repositories
        await _unitOfWork.States.AddAsync(newState);
        await _unitOfWork.Counties.AddAsync(newCounty);
        await _unitOfWork.WaterBodies.AddAsync(newWaterBody);

        var savedCount = await _unitOfWork.SaveChangesAsync();

        // Assert
        savedCount.Should().Be(3);
        newState.Id.Should().BeGreaterThan(0);
        newCounty.Id.Should().BeGreaterThan(0);
        newWaterBody.Id.Should().BeGreaterThan(0);

        // Verify entities were saved and can be retrieved
        var savedState = await _unitOfWork.States.GetByIdAsync(newState.Id);
        var savedCounty = await _unitOfWork.Counties.GetByIdAsync(newCounty.Id);
        var savedWaterBody = await _unitOfWork.WaterBodies.GetByIdAsync(newWaterBody.Id);

        savedState.Should().NotBeNull();
        savedCounty.Should().NotBeNull();
        savedWaterBody.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldReturnTransaction()
    {
        // Act
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        // Assert
        transaction.Should().NotBeNull();
        transaction.Should().BeAssignableTo<IDbContextTransaction>();
    }

    [Fact]
    public async Task Transaction_ShouldRollbackChangesOnDispose()
    {
        // Arrange
        var originalCount = await _unitOfWork.WaterBodies.CountAsync();

        var newWaterBody = new WaterBody
        {
            Name = "Transaction Test Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Start transaction but don't commit
        using (var transaction = await _unitOfWork.BeginTransactionAsync())
        {
            await _unitOfWork.WaterBodies.AddAsync(newWaterBody);
            await _unitOfWork.SaveChangesAsync();
            
            // Verify entity was added within transaction
            var countInTransaction = await _unitOfWork.WaterBodies.CountAsync();
            countInTransaction.Should().Be(originalCount + 1);
            
            // Don't commit - let transaction dispose and rollback
        }

        // Assert - Changes should be rolled back
        var finalCount = await _unitOfWork.WaterBodies.CountAsync();
        finalCount.Should().Be(originalCount);
    }

    [Fact]
    public async Task Transaction_ShouldCommitChangesWhenCommitted()
    {
        // Arrange
        var originalCount = await _unitOfWork.WaterBodies.CountAsync();

        var newWaterBody = new WaterBody
        {
            Name = "Transaction Commit Test Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Start transaction and commit
        using (var transaction = await _unitOfWork.BeginTransactionAsync())
        {
            await _unitOfWork.WaterBodies.AddAsync(newWaterBody);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Assert - Changes should be committed
        var finalCount = await _unitOfWork.WaterBodies.CountAsync();
        finalCount.Should().Be(originalCount + 1);

        // Verify the entity can be retrieved
        var savedEntity = await _unitOfWork.WaterBodies.GetByIdAsync(newWaterBody.Id);
        savedEntity.Should().NotBeNull();
        savedEntity!.Name.Should().Be("Transaction Commit Test Lake");
    }

    [Fact]
    public async Task ComplexBusinessOperation_ShouldMaintainDataConsistency()
    {
        // Arrange - Simulate adding a new water body with its fishing regulation
        var newWaterBody = new WaterBody
        {
            Name = "Complex Operation Lake",
            StateId = 1,
            WaterType = "lake",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        
        // Add water body first
        await _unitOfWork.WaterBodies.AddAsync(newWaterBody);
        await _unitOfWork.SaveChangesAsync(); // This assigns the ID
        
        // Add fishing regulation for the new water body
        var newRegulation = new FishingRegulation
        {
            WaterBodyId = newWaterBody.Id,
            SpeciesId = 1,
            RegulationYear = 2024,
            EffectiveDate = new DateOnly(2024, 1, 1),
            SeasonStartMonth = 5,
            SeasonStartDay = 1,
            SeasonEndMonth = 10,
            SeasonEndDay = 31,
            DailyLimit = 5,
            MinimumSizeInches = 14.0m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.FishingRegulations.AddAsync(newRegulation);
        await _unitOfWork.SaveChangesAsync();
        
        await transaction.CommitAsync();

        // Assert
        newWaterBody.Id.Should().BeGreaterThan(0);
        newRegulation.Id.Should().BeGreaterThan(0);
        newRegulation.WaterBodyId.Should().Be(newWaterBody.Id);

        // Verify both entities are properly related
        var savedWaterBody = await _unitOfWork.WaterBodies.GetByIdAsync(newWaterBody.Id);
        var savedRegulation = await _unitOfWork.FishingRegulations.GetByIdAsync(newRegulation.Id);

        savedWaterBody.Should().NotBeNull();
        savedRegulation.Should().NotBeNull();
        savedRegulation!.WaterBodyId.Should().Be(savedWaterBody!.Id);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleMultipleOperations()
    {
        // Arrange
        var tasks = new List<Task>();
        var waterBodyNames = new List<string>();

        // Act - Simulate concurrent operations
        for (int i = 0; i < 5; i++)
        {
            var waterBodyName = $"Concurrent Lake {i}";
            waterBodyNames.Add(waterBodyName);
            
            tasks.Add(Task.Run(async () =>
            {
                using var uow = new UnitOfWork(Context);
                var waterBody = new WaterBody
                {
                    Name = waterBodyName,
                    StateId = 1,
                    WaterType = "lake",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await uow.WaterBodies.AddAsync(waterBody);
                await uow.SaveChangesAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All water bodies should be created
        foreach (var name in waterBodyNames)
        {
            var waterBodies = await _unitOfWork.WaterBodies.FindAsync(wb => wb.Name == name);
            waterBodies.Should().NotBeEmpty();
        }
    }
}
