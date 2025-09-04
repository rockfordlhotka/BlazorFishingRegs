using Microsoft.EntityFrameworkCore.Storage;
using FishingRegs.Data.Repositories;
using FishingRegs.Data.Repositories.Implementation;

namespace FishingRegs.Data;

/// <summary>
/// Unit of Work implementation for coordinating repository operations
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly FishingRegsDbContext _context;
    private bool _disposed = false;

    // Lazy-loaded repositories
    private IWaterBodyRepository? _waterBodies;
    private IFishingRegulationRepository? _fishingRegulations;
    private IRegulationDocumentRepository? _regulationDocuments;
    private IStateRepository? _states;
    private ICountyRepository? _counties;
    private IFishSpeciesRepository? _fishSpecies;

    public UnitOfWork(FishingRegsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IWaterBodyRepository WaterBodies =>
        _waterBodies ??= new WaterBodyRepository(_context);

    /// <inheritdoc />
    public IFishingRegulationRepository FishingRegulations =>
        _fishingRegulations ??= new FishingRegulationRepository(_context);

    /// <inheritdoc />
    public IRegulationDocumentRepository RegulationDocuments =>
        _regulationDocuments ??= new RegulationDocumentRepository(_context);

    /// <inheritdoc />
    public IStateRepository States =>
        _states ??= new StateRepository(_context);

    /// <inheritdoc />
    public ICountyRepository Counties =>
        _counties ??= new CountyRepository(_context);

    /// <inheritdoc />
    public IFishSpeciesRepository FishSpecies =>
        _fishSpecies ??= new FishSpeciesRepository(_context);

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context.Dispose();
        }
        _disposed = true;
    }
}
