using FishingRegs.Data.Repositories;

namespace FishingRegs.Data;

/// <summary>
/// Unit of Work interface for coordinating repository operations
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Water body repository
    /// </summary>
    IWaterBodyRepository WaterBodies { get; }

    /// <summary>
    /// Fishing regulation repository
    /// </summary>
    IFishingRegulationRepository FishingRegulations { get; }

    /// <summary>
    /// Regulation document repository
    /// </summary>
    IRegulationDocumentRepository RegulationDocuments { get; }

    /// <summary>
    /// State repository
    /// </summary>
    IStateRepository States { get; }

    /// <summary>
    /// County repository
    /// </summary>
    ICountyRepository Counties { get; }

    /// <summary>
    /// Fish species repository
    /// </summary>
    IFishSpeciesRepository FishSpecies { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the database
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entities written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The database transaction</returns>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
