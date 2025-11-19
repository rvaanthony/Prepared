using Prepared.Common.Models;

namespace Prepared.Data.Interfaces;

/// <summary>
/// Repository for managing call records in Azure Table Storage
/// </summary>
public interface ICallRepository
{
    /// <summary>
    /// Saves or updates a call record
    /// </summary>
    Task UpsertAsync(CallInfo callInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a call record by CallSid
    /// </summary>
    Task<CallInfo?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active calls (calls with HasActiveStream = true)
    /// </summary>
    Task<List<CallInfo>> GetActiveCallsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the call status
    /// </summary>
    Task UpdateStatusAsync(string callSid, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the stream information for a call
    /// </summary>
    Task UpdateStreamInfoAsync(string callSid, string? streamSid, bool hasActiveStream, CancellationToken cancellationToken = default);
}

