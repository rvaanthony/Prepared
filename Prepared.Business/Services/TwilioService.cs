using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Common.Enums;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;
using Twilio;
using Twilio.Security;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Stream = Twilio.TwiML.Voice.Stream;
using ThreadingTask = System.Threading.Tasks;

namespace Prepared.Business.Services;

/// <summary>
/// Handles Twilio call orchestration, webhook validation, and status updates.
/// </summary>
public class TwilioService : ITwilioService
{
    private readonly ILogger<TwilioService> _logger;
    private readonly ITwilioConfigurationService _config;
    private readonly ITranscriptHub _transcriptHub;
    private readonly ICallRepository _callRepository;

    public TwilioService(
        ILogger<TwilioService> logger,
        ITwilioConfigurationService config,
        ITranscriptHub transcriptHub,
        ICallRepository callRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        _callRepository = callRepository ?? throw new ArgumentNullException(nameof(callRepository));
        
        // Initialize TwilioClient with API keys
        TwilioClient.Init(username: _config.KeySid, password: _config.SecretKey, accountSid: _config.AccountSid);
        
        _logger.LogInformation("TwilioClient initialized with API Key authentication, using Auth Token for webhook validation");
    }

    /// <summary>
    /// Handles an incoming call from Twilio and generates TwiML response with media stream configuration.
    /// </summary>
    /// <param name="callInfo">Information about the incoming call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>TwiML XML response string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callInfo"/> is null.</exception>
    public async Task<string> HandleIncomingCallAsync(CallInfo callInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callInfo);
        
        try
        {
            _logger.LogInformation(
                "Processing incoming call: CallSid={CallSid}, From={From}, To={To}",
                callInfo.CallSid, callInfo.From, callInfo.To);

            // Save call record to Azure Tables
            try
            {
                await _callRepository.UpsertAsync(callInfo, cancellationToken);
                _logger.LogDebug("Saved call record to storage: CallSid={CallSid}", callInfo.CallSid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save call record, continuing with call processing: CallSid={CallSid}", callInfo.CallSid);
            }

            // Broadcast initial call status to all connected clients so dashboards can discover the new call
            try
            {
                await _transcriptHub.BroadcastCallStatusUpdateAsync(
                    callInfo.CallSid,
                    callInfo.Status.ToString().ToLowerInvariant(),
                    cancellationToken);
                _logger.LogDebug("Broadcast initial call status: CallSid={CallSid}, Status={Status}", 
                    callInfo.CallSid, callInfo.Status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast initial call status: CallSid={CallSid}", callInfo.CallSid);
            }

            var response = new VoiceResponse();
            // Note: Answer verb is implicit for incoming calls that play audio
            // The <Start><Stream> will auto-answer the call before starting the stream
            
            var baseUrl = _config.WebhookUrl.TrimEnd('/');
            // Media streams require WebSocket URL (wss://)
            var mediaStreamUrl = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            mediaStreamUrl = $"{mediaStreamUrl}/api/twilio/media-stream";
            
            // Say a greeting message FIRST (before starting the stream)
            // This ensures the stream only captures the caller's voice, not our greeting
            response.Say(
                _config.GreetingMessage,
                voice: Say.VoiceEnum.PollyJoannaNeural,
                language: Say.LanguageEnum.EnUs);

            // Start media stream AFTER greeting completes - only captures caller's voice
            var start = new Start();
            var stream = new Stream
            {
                Url = mediaStreamUrl,
                Name = callInfo.CallSid, // Use CallSid as stream identifier
                Track = "inbound_track" // Only capture caller's voice, not our greeting
            };
            start.Append(stream);
            response.Append(start);

            // Keep the call alive
            response.Pause(length: 3600); // Max 1 hour pause

            var twiml = response.ToString();
            
            _logger.LogInformation(
                "Generated TwiML response for call: CallSid={CallSid}, TwiML length: {Length}, MediaStream URL: {StreamUrl}",
                callInfo.CallSid, twiml.Length, mediaStreamUrl);
            
            // Log the actual TwiML for debugging
            _logger.LogInformation("Generated TwiML XML: {TwiML}", twiml);

            return await System.Threading.Tasks.Task.FromResult(twiml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing incoming call: CallSid={CallSid}",
                callInfo.CallSid);
            
            // Return a safe TwiML response that hangs up gracefully
            var errorResponse = new VoiceResponse();
            errorResponse.Say(
                _config.ErrorMessage,
                voice: Say.VoiceEnum.PollyJoannaNeural,
                language: Say.LanguageEnum.EnUs);
            errorResponse.Hangup();
            
            return errorResponse.ToString();
        }
    }

    /// <summary>
    /// Handles a call status update webhook from Twilio.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call.</param>
    /// <param name="status">The new call status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callSid"/> or <paramref name="status"/> is null or empty.</exception>
    public async ThreadingTask.Task HandleCallStatusUpdateAsync(string callSid, string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        try
        {
            _logger.LogInformation(
                "Call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);

            // Map Twilio status to our enum
            var callStatus = MapTwilioStatusToCallStatus(status);
            
            // Update call status in storage
            try
            {
                await _callRepository.UpdateStatusAsync(callSid, status, cancellationToken);
                _logger.LogDebug("Updated call status in storage: CallSid={CallSid}, Status={Status}", callSid, status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update call status in storage: CallSid={CallSid}, Status={Status}", callSid, status);
            }

            // Notify connected clients via SignalR
            await _transcriptHub.BroadcastCallStatusUpdateAsync(
                callSid,
                status,
                cancellationToken);
            
            _logger.LogInformation(
                "Processed call status update: CallSid={CallSid}, Status={Status}",
                callSid, callStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);
            throw;
        }
    }

    /// <summary>
    /// Validates a Twilio webhook signature to ensure the request is authentic.
    /// </summary>
    /// <param name="url">The full URL that was called (used for signature validation).</param>
    /// <param name="parameters">The form parameters from the webhook request.</param>
    /// <param name="signature">The X-Twilio-Signature header value.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    public bool ValidateWebhookSignature(string url, Dictionary<string, string> parameters, string signature)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        ArgumentNullException.ThrowIfNull(parameters);
        // Note: Empty/whitespace signatures are handled gracefully by returning false
        // rather than throwing, to match expected behavior for invalid webhook requests
        
        try
        {
            // Handle empty/whitespace signatures gracefully (return false, don't throw)
            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("Webhook signature validation failed: signature is empty");
                return false;
            }

            // Check if validation is disabled (for debugging only - remove in production!)
            if (_config.DisableWebhookValidation)
            {
                _logger.LogWarning("Webhook signature validation is DISABLED - this should only be used for debugging!");
                return true;
            }

            if (string.IsNullOrWhiteSpace(_config.AuthToken))
            {
                _logger.LogError("Webhook signature validation failed: Auth Token is not configured");
                return false;
            }

            // Try the provided URL first
            var validator = new RequestValidator(_config.AuthToken);
            var isValid = validator.Validate(url, parameters, signature);

            if (isValid)
            {
                _logger.LogInformation(
                    "Webhook signature validation succeeded for URL: {Url}. Parameters count: {ParamCount}",
                    url, parameters?.Count ?? 0);
                return true;
            }

            // If validation failed, try URL variations that might match what Twilio configured
            var urlVariations = new List<string> { url };
            
            // Try with trailing slash
            if (!url.EndsWith("/"))
            {
                urlVariations.Add(url + "/");
            }
            else
            {
                urlVariations.Add(url.TrimEnd('/'));
            }

            // Try without query string if present
            if (url.Contains("?"))
            {
                urlVariations.Add(url.Split('?')[0]);
            }

            _logger.LogWarning(
                "Initial validation failed for URL: {Url}. Trying {Count} URL variations. Parameters count: {ParamCount}",
                url, urlVariations.Count, parameters?.Count ?? 0);

            foreach (var urlVariation in urlVariations.Skip(1))
            {
                isValid = validator.Validate(urlVariation, parameters, signature);
                if (isValid)
                {
                    _logger.LogInformation(
                        "Webhook signature validation succeeded with URL variation: {Url}. Original: {OriginalUrl}",
                        urlVariation, url);
                    return true;
                }
            }

            _logger.LogWarning(
                "Webhook signature validation failed for all URL variations. Original URL: {Url}, Parameters count: {ParamCount}",
                url, parameters?.Count ?? 0);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook signature for URL: {Url}", url);
            return false;
        }
    }

    /// <summary>
    /// Gets the configured webhook base URL for Twilio webhooks.
    /// </summary>
    /// <returns>The webhook base URL.</returns>
    public string GetWebhookBaseUrl()
    {
        return _config.WebhookUrl;
    }

    private static CallStatus MapTwilioStatusToCallStatus(string twilioStatus)
    {
        return twilioStatus.ToLowerInvariant() switch
        {
            "queued" => CallStatus.Queued,
            "initiated" => CallStatus.Initiated,
            "ringing" => CallStatus.Ringing,
            "in-progress" => CallStatus.InProgress,
            "completed" => CallStatus.Completed,
            "busy" => CallStatus.Busy,
            "failed" => CallStatus.Failed,
            "no-answer" => CallStatus.NoAnswer,
            "canceled" => CallStatus.Canceled,
            _ => CallStatus.Failed
        };
    }
}

