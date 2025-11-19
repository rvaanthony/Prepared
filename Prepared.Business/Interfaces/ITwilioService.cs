using Prepared.Common.Models;

namespace Prepared.Business.Interfaces;

/// <summary>
/// Service interface for Twilio operations
/// </summary>
public interface ITwilioService
{
    /// <summary>
    /// Processes an incoming call webhook
    /// </summary>
    /// <param name="callInfo">Information about the incoming call</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TwiML response for handling the call</returns>
    Task<string> HandleIncomingCallAsync(CallInfo callInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a call status update webhook
    /// </summary>
    /// <param name="callSid">The call SID</param>
    /// <param name="status">The new call status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleCallStatusUpdateAsync(string callSid, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a Twilio webhook request signature
    /// </summary>
    /// <param name="url">The webhook URL</param>
    /// <param name="parameters">The request parameters</param>
    /// <param name="signature">The signature to validate</param>
    /// <returns>True if the signature is valid</returns>
    bool ValidateWebhookSignature(string url, Dictionary<string, string> parameters, string signature);
}

