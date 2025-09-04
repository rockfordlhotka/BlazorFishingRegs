using Microsoft.EntityFrameworkCore;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories;

namespace FishingRegs.Data.Repositories.Implementation;

/// <summary>
/// Repository implementation for State entity operations
/// </summary>
public class StateRepository : Repository<State>, IStateRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public StateRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<State?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<State>> GetByCountryAsync(string country, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.Country == country)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<State?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(s => s.Counties)
            .Include(s => s.WaterBodies.Where(wb => wb.IsActive))
            .Include(s => s.RegulationDocuments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<State>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Repository implementation for County entity operations
/// </summary>
public class CountyRepository : Repository<County>, ICountyRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public CountyRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<County>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.StateId == stateId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<County?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.State)
            .Include(c => c.WaterBodies.Where(wb => wb.IsActive))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<County>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.State)
            .OrderBy(c => c.State.Name)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Repository implementation for FishSpecies entity operations
/// </summary>
public class FishSpeciesRepository : Repository<FishSpecies>, IFishSpeciesRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public FishSpeciesRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishSpecies>> SearchByNameAsync(string namePattern, CancellationToken cancellationToken = default)
    {
        // Use Contains for in-memory database compatibility (case-insensitive search)
        var pattern = namePattern.ToLowerInvariant();
        return await _dbSet
            .Where(fs => fs.IsActive && 
                         (fs.CommonName.ToLower().Contains(pattern) ||
                          (fs.ScientificName != null && fs.ScientificName.ToLower().Contains(pattern))))
            .OrderBy(fs => fs.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishSpecies>> GetByGameFishStatusAsync(bool isGameFish, CancellationToken cancellationToken = default)
    {
        // This would require a GameFish property on FishSpecies model
        // For now, we'll return all active species and let the business logic determine game fish status
        return await _dbSet
            .Where(fs => fs.IsActive)
            .OrderBy(fs => fs.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FishSpecies?> GetByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(fs => fs.ScientificName == scientificName, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<FishSpecies>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fs => fs.IsActive)
            .OrderBy(fs => fs.CommonName)
            .ToListAsync(cancellationToken);
    }
}
