using FishingRegs.Data.Models;

namespace FishingRegs.Data.Repositories;

/// <summary>
/// Repository interface for State entity operations
/// </summary>
public interface IStateRepository : IRepository<State>
{
    /// <summary>
    /// Gets a state by its code (e.g., "MN", "WI")
    /// </summary>
    /// <param name="code">The state code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The state with the specified code or null</returns>
    Task<State?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets states by country
    /// </summary>
    /// <param name="country">The country code (e.g., "US", "CA")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of states in the specified country</returns>
    Task<IEnumerable<State>> GetByCountryAsync(string country, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a state with its related counties and water bodies
    /// </summary>
    /// <param name="id">The state ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State with related data or null if not found</returns>
    Task<State?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for County entity operations
/// </summary>
public interface ICountyRepository : IRepository<County>
{
    /// <summary>
    /// Gets counties by state
    /// </summary>
    /// <param name="stateId">The state ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of counties in the specified state</returns>
    Task<IEnumerable<County>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a county with its related state and water bodies
    /// </summary>
    /// <param name="id">The county ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>County with related data or null if not found</returns>
    Task<County?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for FishSpecies entity operations
/// </summary>
public interface IFishSpeciesRepository : IRepository<FishSpecies>
{
    /// <summary>
    /// Gets fish species by common name pattern
    /// </summary>
    /// <param name="namePattern">The name pattern to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of fish species matching the name pattern</returns>
    Task<IEnumerable<FishSpecies>> SearchByNameAsync(string namePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fish species by game fish classification
    /// </summary>
    /// <param name="isGameFish">Whether to get game fish or non-game fish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of fish species by game fish classification</returns>
    Task<IEnumerable<FishSpecies>> GetByGameFishStatusAsync(bool isGameFish, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a fish species by its scientific name
    /// </summary>
    /// <param name="scientificName">The scientific name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The fish species with the specified scientific name or null</returns>
    Task<FishSpecies?> GetByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default);
}
