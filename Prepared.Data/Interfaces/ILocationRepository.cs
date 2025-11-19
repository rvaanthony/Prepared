using Prepared.Common.Models;

namespace Prepared.Data.Interfaces;

/// <summary>
/// Repository for managing extracted locations in Azure Table Storage
/// </summary>
public interface ILocationRepository
{
    /// <summary>
    /// Saves or updates a location extraction result
    /// </summary>
    Task UpsertAsync(LocationExtractionResult location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a location for a call
    /// </summary>
    Task<LocationExtractionResult?> GetByCallSidAsync(string callSid, CancellationToken cancellationToken = default);
}

