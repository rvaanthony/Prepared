using Prepared.Business.Services;

namespace Prepared.Business.Interfaces;

/// <summary>
/// Unified insights extraction service interface that extracts location, summary, and key findings in a single API call.
/// This is more efficient and cost-effective than separate calls, and allows GPT to correlate information.
/// </summary>
public interface IUnifiedInsightsService
{
    /// <summary>
    /// Extracts unified insights (location, summary, key findings) from a call transcript.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call</param>
    /// <param name="transcript">The transcript text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Unified insights result containing location, summary, and key findings, or null if extraction fails</returns>
    Task<UnifiedInsightsResult?> ExtractInsightsAsync(
        string callSid, 
        string transcript, 
        CancellationToken cancellationToken = default);
}

