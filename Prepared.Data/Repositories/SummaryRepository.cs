using Microsoft.Extensions.Logging;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Repositories;

/// <summary>
/// Repository implementation for managing call summaries in Azure Table Storage
/// </summary>
public class SummaryRepository : ISummaryRepository
{
    private readonly ITableStorageService _tableStorage;
    private readonly ILogger<SummaryRepository> _logger;

    public SummaryRepository(ITableStorageService tableStorage, ILogger<SummaryRepository> logger)
    {
        _tableStorage = tableStorage ?? throw new ArgumentNullException(nameof(tableStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UpsertAsync(TranscriptSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        try
        {
            var entity = SummaryEntity.FromTranscriptSummary(summary);
            await _tableStorage.UpsertEntityAsync(SummaryEntity.TableName, entity, cancellationToken);
            _logger.LogDebug("Upserted summary: CallSid={CallSid}", summary.CallSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert summary: CallSid={CallSid}", summary.CallSid);
            throw;
        }
    }

    public async Task<TranscriptSummary?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var entity = await _tableStorage.GetEntityAsync<SummaryEntity>(
                SummaryEntity.TableName,
                callSid.ToLowerInvariant(),
                SummaryEntity.RowKeyValue,
                cancellationToken);

            return entity?.ToTranscriptSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve summary: CallSid={CallSid}", callSid);
            throw;
        }
    }
}

