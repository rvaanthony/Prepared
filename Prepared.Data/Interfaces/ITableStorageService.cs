using Azure.Data.Tables;

namespace Prepared.Data.Interfaces;

public interface ITableStorageService
{
    Task EnsureTableExistsAsync(string tableName, CancellationToken cancellationToken = default);
    Task UpsertEntityAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default);
    Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();
    Task<List<T>> QueryEntitiesAsync<T>(string tableName, string filterQuery, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();
    Task<(List<T> Entities, string? ContinuationToken)> QueryEntitiesWithPaginationAsync<T>(string tableName, string filterQuery, string? continuationToken = null, int? pageSize = null, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    /// <summary>
    /// Inserts an entity only if it doesn't exist (atomic operation using ETag).
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the entity was inserted (didn't exist), false if it already exists.</returns>
    Task<bool> InsertIfNotExistsAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an entity with ETag-based optimistic concurrency. Will fail if ETag doesn't match (entity was modified by another instance).
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="entity">The entity to update (must have ETag set from the original entity).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if update succeeded, false if ETag mismatch (another instance updated it first).</returns>
    Task<bool> UpdateEntityWithETagAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default);
}
