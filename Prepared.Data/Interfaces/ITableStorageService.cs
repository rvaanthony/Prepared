using Azure.Data.Tables;

namespace Prepared.Data.Interfaces;

/// <summary>
/// Service interface for Azure Table Storage operations.
/// Provides abstraction over Azure Data Tables for entity persistence and retrieval.
/// </summary>
public interface ITableStorageService
{
    /// <summary>
    /// Ensures that a table exists in Azure Table Storage, creating it if necessary.
    /// </summary>
    /// <param name="tableName">The name of the table to ensure exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tableName"/> is null or whitespace.</exception>
    Task EnsureTableExistsAsync(string tableName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Inserts or updates an entity in the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    Task UpsertEntityAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an entity from the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="partitionKey">The partition key of the entity to delete.</param>
    /// <param name="rowKey">The row key of the entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a single entity from the specified table.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="partitionKey">The partition key of the entity.</param>
    /// <param name="rowKey">The row key of the entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();
    
    /// <summary>
    /// Queries entities from the specified table using a filter query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="filterQuery">The OData filter query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching entities.</returns>
    Task<List<T>> QueryEntitiesAsync<T>(string tableName, string filterQuery, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();
    
    /// <summary>
    /// Queries entities from the specified table with pagination support.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="filterQuery">The OData filter query string.</param>
    /// <param name="continuationToken">The continuation token from a previous query, or null for the first page.</param>
    /// <param name="pageSize">The maximum number of entities to return per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the list of entities and the continuation token for the next page.</returns>
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
