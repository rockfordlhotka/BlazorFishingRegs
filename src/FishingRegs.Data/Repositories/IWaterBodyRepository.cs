using FishingRegs.Data.Models;

namespace FishingRegs.Data.Repositories;

/// <summary>
/// Repository interface for WaterBody entity operations
/// </summary>
public interface IWaterBodyRepository : IRepository<WaterBody>
{
    /// <summary>
    /// Gets water bodies by state
    /// </summary>
    /// <param name="stateId">The state ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies in the specified state</returns>
    Task<IEnumerable<WaterBody>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets water bodies by county
    /// </summary>
    /// <param name="countyId">The county ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies in the specified county</returns>
    Task<IEnumerable<WaterBody>> GetByCountyAsync(int countyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets water bodies by type (lake, river, stream, etc.)
    /// </summary>
    /// <param name="waterType">The water body type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies of the specified type</returns>
    Task<IEnumerable<WaterBody>> GetByTypeAsync(string waterType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches water bodies by name pattern
    /// </summary>
    /// <param name="namePattern">The name pattern to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies matching the name pattern</returns>
    Task<IEnumerable<WaterBody>> SearchByNameAsync(string namePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets water bodies within a geographic area
    /// </summary>
    /// <param name="minLatitude">Minimum latitude</param>
    /// <param name="maxLatitude">Maximum latitude</param>
    /// <param name="minLongitude">Minimum longitude</param>
    /// <param name="maxLongitude">Maximum longitude</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies within the specified bounds</returns>
    Task<IEnumerable<WaterBody>> GetByGeographicAreaAsync(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets water bodies with their related state and county information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of water bodies with navigation properties loaded</returns>
    Task<IEnumerable<WaterBody>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a water body by ID with related data (state, county, species, regulations)
    /// </summary>
    /// <param name="id">The water body ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Water body with related data or null if not found</returns>
    Task<WaterBody?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default);
}
