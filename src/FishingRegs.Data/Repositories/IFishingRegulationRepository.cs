using FishingRegs.Data.Models;

namespace FishingRegs.Data.Repositories;

/// <summary>
/// Repository interface for FishingRegulation entity operations
/// </summary>
public interface IFishingRegulationRepository : IRepository<FishingRegulation>
{
    /// <summary>
    /// Gets fishing regulations for a specific water body
    /// </summary>
    /// <param name="waterBodyId">The water body ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of fishing regulations for the water body</returns>
    Task<IEnumerable<FishingRegulation>> GetByWaterBodyAsync(int waterBodyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fishing regulations for a specific fish species
    /// </summary>
    /// <param name="fishSpeciesId">The fish species ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of fishing regulations for the species</returns>
    Task<IEnumerable<FishingRegulation>> GetByFishSpeciesAsync(int fishSpeciesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current/active fishing regulations (not expired)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of active fishing regulations</returns>
    Task<IEnumerable<FishingRegulation>> GetActiveRegulationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fishing regulations that are effective on a specific date
    /// </summary>
    /// <param name="effectiveDate">The date to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations effective on the specified date</returns>
    Task<IEnumerable<FishingRegulation>> GetByEffectiveDateAsync(DateTime effectiveDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fishing regulations for a water body and species combination
    /// </summary>
    /// <param name="waterBodyId">The water body ID</param>
    /// <param name="fishSpeciesId">The fish species ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations for the specific water body and species</returns>
    Task<IEnumerable<FishingRegulation>> GetByWaterBodyAndSpeciesAsync(
        int waterBodyId,
        int fishSpeciesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fishing regulations from a specific regulation document
    /// </summary>
    /// <param name="documentId">The regulation document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations from the document</returns>
    Task<IEnumerable<FishingRegulation>> GetByDocumentAsync(int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fishing regulations with their related data (water body, species, document)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations with navigation properties loaded</returns>
    Task<IEnumerable<FishingRegulation>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a fishing regulation by ID with related data
    /// </summary>
    /// <param name="id">The regulation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Regulation with related data or null if not found</returns>
    Task<FishingRegulation?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulations that have seasonal restrictions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations with seasonal restrictions</returns>
    Task<IEnumerable<FishingRegulation>> GetSeasonalRegulationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulations with size or bag limits
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulations with size or bag limits</returns>
    Task<IEnumerable<FishingRegulation>> GetWithLimitsAsync(CancellationToken cancellationToken = default);
}
