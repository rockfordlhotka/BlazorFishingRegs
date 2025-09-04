using System.Linq.Expressions;

namespace FishingRegs.Data.Repositories;

/// <summary>
/// Base repository interface providing common CRUD operations
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets an entity by its primary key
    /// </summary>
    /// <param name="id">The entity's primary key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all entities</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the specified predicate
    /// </summary>
    /// <param name="predicate">The search predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching entities</returns>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity matching the specified predicate
    /// </summary>
    /// <param name="predicate">The search predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity or null</returns>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added entity</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities
    /// </summary>
    /// <param name="entities">The entities to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity
    /// </summary>
    /// <param name="entity">The entity to update</param>
    void Update(TEntity entity);

    /// <summary>
    /// Removes an entity
    /// </summary>
    /// <param name="entity">The entity to remove</param>
    void Remove(TEntity entity);

    /// <summary>
    /// Removes multiple entities
    /// </summary>
    /// <param name="entities">The entities to remove</param>
    void RemoveRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Gets the count of entities matching the predicate
    /// </summary>
    /// <param name="predicate">The search predicate (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of matching entities</returns>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the predicate
    /// </summary>
    /// <param name="predicate">The search predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entity matches, false otherwise</returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
