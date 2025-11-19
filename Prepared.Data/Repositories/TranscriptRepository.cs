using Microsoft.Extensions.Logging;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Repositories;

/// <summary>
/// Repository implementation for managing transcript chunks in Azure Table Storage
/// </summary>
public class TranscriptRepository : ITranscriptRepository
{
    private readonly ITableStorageService _tableStorage;
    private readonly ILogger<TranscriptRepository> _logger;

    public TranscriptRepository(ITableStorageService tableStorage, ILogger<TranscriptRepository> logger)
    {
        _tableStorage = tableStorage ?? throw new ArgumentNullException(nameof(tableStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveAsync(TranscriptionResult transcription, int sequenceNumber = 0, CancellationToken cancellationToken = default)
    {
        if (transcription == null)
            throw new ArgumentNullException(nameof(transcription));

        try
        {
            var entity = TranscriptEntity.FromTranscriptionResult(transcription, sequenceNumber);
            await _tableStorage.UpsertEntityAsync(TranscriptEntity.TableName, entity, cancellationToken);
            _logger.LogDebug("Saved transcript chunk: CallSid={CallSid}, IsFinal={IsFinal}, Sequence={Sequence}",
                transcription.CallSid, transcription.IsFinal, sequenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save transcript chunk: CallSid={CallSid}", transcription.CallSid);
            throw;
        }
    }

    public async Task<List<TranscriptionResult>> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var filter = $"PartitionKey eq '{callSid.ToLowerInvariant()}'";
            var entities = await _tableStorage.QueryEntitiesAsync<TranscriptEntity>(
                TranscriptEntity.TableName,
                filter,
                cancellationToken);

            // Sort by sequence number and timestamp
            var sorted = entities
                .OrderBy(e => e.SequenceNumber)
                .ThenBy(e => e.TimestampUtc)
                .Select(e => e.ToTranscriptionResult())
                .ToList();

            _logger.LogDebug("Retrieved {Count} transcript chunks for CallSid={CallSid}", sorted.Count, callSid);
            return sorted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve transcript chunks: CallSid={CallSid}", callSid);
            throw;
        }
    }

    public async Task<List<TranscriptionResult>> GetFinalTranscriptsAsync(string callSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));

        try
        {
            var filter = $"PartitionKey eq '{callSid.ToLowerInvariant()}' and IsFinal eq true";
            var entities = await _tableStorage.QueryEntitiesAsync<TranscriptEntity>(
                TranscriptEntity.TableName,
                filter,
                cancellationToken);

            var sorted = entities
                .OrderBy(e => e.SequenceNumber)
                .ThenBy(e => e.TimestampUtc)
                .Select(e => e.ToTranscriptionResult())
                .ToList();

            _logger.LogDebug("Retrieved {Count} final transcript chunks for CallSid={CallSid}", sorted.Count, callSid);
            return sorted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve final transcript chunks: CallSid={CallSid}", callSid);
            throw;
        }
    }

    public async Task<string> GetFullTranscriptTextAsync(string callSid, CancellationToken cancellationToken = default)
    {
        var finalTranscripts = await GetFinalTranscriptsAsync(callSid, cancellationToken);
        return string.Join(" ", finalTranscripts.Select(t => t.Text));
    }
}

