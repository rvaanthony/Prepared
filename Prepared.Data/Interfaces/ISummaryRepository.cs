using Prepared.Common.Models;

namespace Prepared.Data.Interfaces;

/// <summary>
/// Repository for managing call summaries in Azure Table Storage
/// </summary>
public interface ISummaryRepository
{
    /// <summary>
    /// Saves or updates a call summary
    /// </summary>
    Task UpsertAsync(TranscriptSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary for a call
    /// </summary>
    Task<TranscriptSummary?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default);
}

