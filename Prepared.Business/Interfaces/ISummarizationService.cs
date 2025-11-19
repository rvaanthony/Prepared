using Prepared.Common.Models;

namespace Prepared.Business.Interfaces;

public interface ISummarizationService
{
    Task<TranscriptSummary?> SummarizeAsync(string callSid, string transcript, CancellationToken cancellationToken = default);
}

