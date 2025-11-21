using Microsoft.Extensions.Logging;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Repositories;

/// <summary>
/// Repository implementation for managing call records in Azure Table Storage
/// </summary>
public class CallRepository : ICallRepository
{
    private readonly ITableStorageService _tableStorage;
    private readonly ILogger<CallRepository> _logger;

    public CallRepository(ITableStorageService tableStorage, ILogger<CallRepository> logger)
    {
        _tableStorage = tableStorage ?? throw new ArgumentNullException(nameof(tableStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UpsertAsync(CallInfo callInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callInfo);

        try
        {
            var entity = CallEntity.FromCallInfo(callInfo);
            await _tableStorage.UpsertEntityAsync(CallEntity.TableName, entity, cancellationToken);
            _logger.LogDebug("Upserted call record: CallSid={CallSid}", callInfo.CallSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert call record: CallSid={CallSid}", callInfo.CallSid);
            throw;
        }
    }

    public async Task<CallInfo?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var entity = await _tableStorage.GetEntityAsync<CallEntity>(
                CallEntity.TableName,
                callSid.ToLowerInvariant(),
                CallEntity.RowKeyValue,
                cancellationToken);

            return entity?.ToCallInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve call record: CallSid={CallSid}", callSid);
            throw;
        }
    }

    public async Task<List<CallInfo>> GetActiveCallsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Query for calls where HasActiveStream = true
            var filter = $"HasActiveStream eq true";
            var entities = await _tableStorage.QueryEntitiesAsync<CallEntity>(
                CallEntity.TableName,
                filter,
                cancellationToken);

            return entities.Select(e => e.ToCallInfo()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active calls");
            throw;
        }
    }

    public async Task UpdateStatusAsync(string callSid, string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        try
        {
            var existing = await GetByCallSidAsync(callSid, cancellationToken);
            if (existing is null)
            {
                _logger.LogWarning("Call not found for status update: CallSid={CallSid}", callSid);
                return;
            }

            existing.Status = Enum.TryParse<Common.Enums.CallStatus>(status, out var parsedStatus)
                ? parsedStatus
                : Common.Enums.CallStatus.Failed;

            await UpsertAsync(existing, cancellationToken);
            _logger.LogDebug("Updated call status: CallSid={CallSid}, Status={Status}", callSid, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update call status: CallSid={CallSid}, Status={Status}", callSid, status);
            throw;
        }
    }

    public async Task UpdateStreamInfoAsync(string callSid, string? streamSid, bool hasActiveStream, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var existing = await GetByCallSidAsync(callSid, cancellationToken);
            if (existing is null)
            {
                _logger.LogWarning("Call not found for stream info update: CallSid={CallSid}", callSid);
                return;
            }

            existing.HasActiveStream = hasActiveStream;
            var entity = CallEntity.FromCallInfo(existing);
            entity.StreamSid = streamSid;

            await _tableStorage.UpsertEntityAsync(CallEntity.TableName, entity, cancellationToken);
            _logger.LogDebug("Updated call stream info: CallSid={CallSid}, StreamSid={StreamSid}, HasActiveStream={HasActiveStream}",
                callSid, streamSid, hasActiveStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update call stream info: CallSid={CallSid}", callSid);
            throw;
        }
    }
}

