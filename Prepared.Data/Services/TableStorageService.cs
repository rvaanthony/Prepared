using Azure;
using Azure.Data.Tables;
using Prepared.Data.Interfaces;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Prepared.Data.Services;

public class TableStorageService : ITableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger<TableStorageService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _tableCreationLocks = new();

    public TableStorageService(TableServiceClient tableServiceClient, ILogger<TableStorageService> logger)
    {
        _tableServiceClient = tableServiceClient ?? throw new ArgumentNullException(nameof(tableServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureTableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));
        }

        var tableLock = _tableCreationLocks.GetOrAdd(tableName, _ => new SemaphoreSlim(1, 1));

        await tableLock.WaitAsync(cancellationToken);
        try
        {
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync(cancellationToken);
            _logger.LogInformation("Ensured table {TableName} exists.", tableName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to create or check existence of table {TableName}.", tableName);
            throw;
        }
        finally
        {
            tableLock.Release();
        }
    }

    public async Task UpsertEntityAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await EnsureTableExistsAsync(tableName, cancellationToken);
        var tableClient = _tableServiceClient.GetTableClient(tableName);

        try
        {
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
            _logger.LogInformation("Upserted entity {PartitionKey}:{RowKey} into table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upsert entity {PartitionKey}:{RowKey} in table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
            throw;
        }
        catch (Exception)
        {
            // Swallow non-RequestFailedException errors for upsert operations
        }
    }

    public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partitionKey) || string.IsNullOrWhiteSpace(rowKey))
        {
            throw new ArgumentException("PartitionKey and RowKey cannot be null or whitespace.");
        }

        await EnsureTableExistsAsync(tableName, cancellationToken);
        var tableClient = _tableServiceClient.GetTableClient(tableName);

        try
        {
            await tableClient.DeleteEntityAsync(partitionKey.ToLowerInvariant(), rowKey.ToLowerInvariant(), cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted entity {PartitionKey}:{RowKey} from table {TableName}.", partitionKey, rowKey, tableName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Entity {PartitionKey}:{RowKey} not found in table {TableName}.", partitionKey, rowKey, tableName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete entity {PartitionKey}:{RowKey} from table {TableName}.", partitionKey, rowKey, tableName);
            throw;
        }
    }

    public async Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        try
        {
            var response = await tableClient.GetEntityAsync<T>(partitionKey.ToLowerInvariant(), rowKey.ToLowerInvariant(), cancellationToken: cancellationToken);
            _logger.LogInformation("Retrieved entity {PartitionKey}:{RowKey} from table {TableName}.", partitionKey, rowKey, tableName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Entity {PartitionKey}:{RowKey} not found in table {TableName}.", partitionKey, rowKey, tableName);
            return null;
        }
    }

    public async Task<List<T>> QueryEntitiesAsync<T>(string tableName, string filterQuery, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var entities = new List<T>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<T>(filterQuery, cancellationToken: cancellationToken))
            {
                entities.Add(entity);
            }

            _logger.LogInformation("Queried {EntityCount} entities from table {TableName}.", entities.Count, tableName);
            return entities;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to query entities in table {TableName}.", tableName);
            throw;
        }
    }

    public async Task<(List<T> Entities, string? ContinuationToken)> QueryEntitiesWithPaginationAsync<T>(string tableName, string filterQuery, string? continuationToken = null, int? pageSize = null, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var entities = new List<T>();

        try
        {
            var pages = tableClient.QueryAsync<T>(filterQuery, pageSize, cancellationToken: cancellationToken).AsPages(continuationToken);
            await foreach (var page in pages)
            {
                entities.AddRange(page.Values);
                continuationToken = page.ContinuationToken;
            }

            _logger.LogInformation("Queried {EntityCount} entities from table {TableName}.", entities.Count, tableName);
            return (entities, continuationToken);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to query entities with pagination in table {TableName}.", tableName);
            throw;
        }
    }

    public async Task<bool> InsertIfNotExistsAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await EnsureTableExistsAsync(tableName, cancellationToken);
        var tableClient = _tableServiceClient.GetTableClient(tableName);

        try
        {
            // AddEntityAsync will throw 409 Conflict if entity already exists (atomic operation)
            // ETag is not needed for insert operations, only for updates
            await tableClient.AddEntityAsync(entity, cancellationToken: cancellationToken);
            _logger.LogInformation("Inserted new entity {PartitionKey}:{RowKey} into table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409) // Conflict - entity already exists
        {
            _logger.LogDebug("Entity {PartitionKey}:{RowKey} already exists in table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
            return false;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to insert entity {PartitionKey}:{RowKey} in table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
            throw;
        }
    }

    public async Task<bool> UpdateEntityWithETagAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await EnsureTableExistsAsync(tableName, cancellationToken);
        var tableClient = _tableServiceClient.GetTableClient(tableName);

        try
        {
            // UpdateEntityAsync with Replace mode and ETag check
            // This will fail with 412 Precondition Failed if ETag doesn't match
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated entity {PartitionKey}:{RowKey} in table {TableName} with ETag check.",
                entity.PartitionKey, entity.RowKey, tableName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412) // Precondition Failed - ETag mismatch
        {
            _logger.LogDebug("Update failed for entity {PartitionKey}:{RowKey} in table {TableName} - ETag mismatch (another instance updated it first).",
                entity.PartitionKey, entity.RowKey, tableName);
            return false;
        }
        catch (RequestFailedException ex) when (ex.Status == 404) // Not Found
        {
            _logger.LogDebug("Update failed for entity {PartitionKey}:{RowKey} in table {TableName} - entity not found.",
                entity.PartitionKey, entity.RowKey, tableName);
            return false;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to update entity {PartitionKey}:{RowKey} in table {TableName}.",
                entity.PartitionKey, entity.RowKey, tableName);
            throw;
        }
    }
}