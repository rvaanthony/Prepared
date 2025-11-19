using Prepared.Common.Models;

namespace Prepared.Business.Interfaces;

/// <summary>
/// Defines the contract for audio transcription services.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Processes a chunk of audio and returns a transcription result, if available.
    /// </summary>
    /// <param name="callSid">The Twilio Call SID.</param>
    /// <param name="streamSid">The Twilio Stream SID.</param>
    /// <param name="audioBytes">The raw audio bytes (PCM/PCMU) for the chunk.</param>
    /// <param name="isFinal">Indicates whether this is the final chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcription result, or null if no text was produced.</returns>
    Task<TranscriptionResult?> TranscribeAsync(
        string callSid,
        string streamSid,
        ReadOnlyMemory<byte> audioBytes,
        bool isFinal = false,
        CancellationToken cancellationToken = default);
}

