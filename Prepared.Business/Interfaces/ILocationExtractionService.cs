using Prepared.Common.Models;

namespace Prepared.Business.Interfaces;

public interface ILocationExtractionService
{
    Task<LocationExtractionResult?> ExtractAsync(string callSid, string transcript, CancellationToken cancellationToken = default);
}

