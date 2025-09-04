using Microsoft.EntityFrameworkCore;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories;

namespace FishingRegs.Data.Repositories.Implementation;

/// <summary>
/// Repository implementation for FishingRegulation entity operations
/// </summary>
public class FishingRegulationRepository : Repository<FishingRegulation>, IFishingRegulationRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public FishingRegulationRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetByWaterBodyAsync(int waterBodyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.WaterBodyId == waterBodyId && fr.IsActive)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.Species.CommonName)
            .ThenBy(fr => fr.RegulationYear)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetByFishSpeciesAsync(int fishSpeciesId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.SpeciesId == fishSpeciesId && fr.IsActive)
            .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.State)
            .OrderBy(fr => fr.WaterBody.State.Name)
            .ThenBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.RegulationYear)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetActiveRegulationsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return await _dbSet
            .Where(fr => fr.IsActive && 
                         (fr.ExpirationDate == null || fr.ExpirationDate > today))
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetByEffectiveDateAsync(DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(effectiveDate);

        return await _dbSet
            .Where(fr => fr.IsActive && 
                         fr.EffectiveDate <= date &&
                         (fr.ExpirationDate == null || fr.ExpirationDate > date))
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetByWaterBodyAndSpeciesAsync(
        int waterBodyId,
        int fishSpeciesId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.WaterBodyId == waterBodyId && 
                         fr.SpeciesId == fishSpeciesId && 
                         fr.IsActive)
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .Include(fr => fr.SourceDocument)
            .OrderByDescending(fr => fr.RegulationYear)
            .ThenByDescending(fr => fr.EffectiveDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetByDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.SourceDocumentId == Guid.Parse(documentId.ToString()) && fr.IsActive)
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.IsActive)
            .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.State)
            .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.County)
            .Include(fr => fr.Species)
            .Include(fr => fr.SourceDocument)
            .OrderBy(fr => fr.WaterBody.State.Name)
            .ThenBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FishingRegulation?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.State)
            .Include(fr => fr.WaterBody)
                .ThenInclude(wb => wb.County)
            .Include(fr => fr.Species)
            .Include(fr => fr.SourceDocument)
            .FirstOrDefaultAsync(fr => fr.Id == Guid.Parse(id.ToString()), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetSeasonalRegulationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.IsActive && 
                         !fr.IsYearRound &&
                         (fr.SeasonOpenDate.HasValue || fr.SeasonCloseDate.HasValue))
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.SeasonOpenDate)
            .ThenBy(fr => fr.WaterBody.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FishingRegulation>> GetWithLimitsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.IsActive && 
                         (fr.DailyLimit.HasValue || 
                          fr.PossessionLimit.HasValue ||
                          fr.MinimumSizeInches.HasValue ||
                          fr.MaximumSizeInches.HasValue ||
                          fr.ProtectedSlotMinInches.HasValue))
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<FishingRegulation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(fr => fr.IsActive)
            .Include(fr => fr.WaterBody)
            .Include(fr => fr.Species)
            .OrderBy(fr => fr.WaterBody.Name)
            .ThenBy(fr => fr.Species.CommonName)
            .ToListAsync(cancellationToken);
    }
}
