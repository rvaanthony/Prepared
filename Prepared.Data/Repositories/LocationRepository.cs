using Microsoft.Extensions.Logging;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Repositories;

/// <summary>
/// Repository implementation for managing extracted locations in Azure Table Storage
/// </summary>
public class LocationRepository : ILocationRepository
{
    private readonly ITableStorageService _tableStorage;
    private readonly ILogger<LocationRepository> _logger;

    public LocationRepository(ITableStorageService tableStorage, ILogger<LocationRepository> logger)
    {
        _tableStorage = tableStorage ?? throw new ArgumentNullException(nameof(tableStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UpsertAsync(LocationExtractionResult location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        try
        {
            var entity = LocationEntity.FromLocationExtractionResult(location);
            await _tableStorage.UpsertEntityAsync(LocationEntity.TableName, entity, cancellationToken);
            _logger.LogDebug("Upserted location: CallSid={CallSid}, Lat={Latitude}, Lng={Longitude}",
                location.CallSid, location.Latitude, location.Longitude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert location: CallSid={CallSid}", location.CallSid);
            throw;
        }
    }

    public async Task<LocationExtractionResult?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var entity = await _tableStorage.GetEntityAsync<LocationEntity>(
                LocationEntity.TableName,
                callSid.ToLowerInvariant(),
                LocationEntity.RowKeyValue,
                cancellationToken);

            return entity?.ToLocationExtractionResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve location: CallSid={CallSid}", callSid);
            throw;
        }
    }
}

