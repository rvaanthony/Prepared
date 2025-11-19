using Prepared.Common.Models;

namespace Prepared.Data.Interfaces;

/// <summary>
/// Repository for managing transcript chunks in Azure Table Storage
/// </summary>
public interface ITranscriptRepository
{
    /// <summary>
    /// Saves a transcript chunk
    /// </summary>
    Task SaveAsync(TranscriptionResult transcription, int sequenceNumber = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all transcript chunks for a call, ordered by timestamp
    /// </summary>
    Task<List<TranscriptionResult>> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves only final transcript chunks for a call
    /// </summary>
    Task<List<TranscriptionResult>> GetFinalTranscriptsAsync(string callSid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full transcript text for a call (concatenated final chunks)
    /// </summary>
    Task<string> GetFullTranscriptTextAsync(string callSid, CancellationToken cancellationToken = default);
}

