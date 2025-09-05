using Microsoft.EntityFrameworkCore;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories;

namespace FishingRegs.Data.Repositories.Implementation;

/// <summary>
/// Repository implementation for WaterBody entity operations
/// </summary>
public class WaterBodyRepository : Repository<WaterBody>, IWaterBodyRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public WaterBodyRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.StateId == stateId && wb.IsActive)
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> GetByCountyAsync(int countyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.CountyId == countyId && wb.IsActive)
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> GetByTypeAsync(string waterType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.WaterType == waterType && wb.IsActive)
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> SearchByNameAsync(string namePattern, CancellationToken cancellationToken = default)
    {
        // Note: EF.Functions.ILike is PostgreSQL-specific and not supported by InMemory provider
        // For compatibility with testing, we'll use regular Contains for pattern matching
        // In production with PostgreSQL, this should use ILike for case-insensitive matching
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.IsActive && wb.Name.ToLower().Contains(namePattern.ToLower()))
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> GetByGeographicAreaAsync(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.IsActive &&
                         wb.Latitude.HasValue &&
                         wb.Longitude.HasValue &&
                         wb.Latitude >= minLatitude &&
                         wb.Latitude <= maxLatitude &&
                         wb.Longitude >= minLongitude &&
                         wb.Longitude <= maxLongitude)
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WaterBody>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Where(wb => wb.IsActive)
            .OrderBy(wb => wb.State.Name)
            .ThenBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WaterBody?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .Include(wb => wb.FishingRegulations.Where(fr => fr.IsActive))
                .ThenInclude(fr => fr.Species)
            .FirstOrDefaultAsync(wb => wb.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets a water body by ID including only State and County information (avoids missing tables)
    /// </summary>
    public async Task<WaterBody?> GetWaterBodyWithStateAndCountyAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wb => wb.State)
            .Include(wb => wb.County)
            .FirstOrDefaultAsync(wb => wb.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<WaterBody>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wb => wb.IsActive)
            .OrderBy(wb => wb.Name)
            .ToListAsync(cancellationToken);
    }
}
