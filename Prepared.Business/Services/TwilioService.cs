using Microsoft.Extensions.Configuration;
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
/// Service for handling Twilio operations with production-ready error handling and logging
/// </summary>
public class TwilioService : ITwilioService
{
    private readonly ILogger<TwilioService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITranscriptHub _transcriptHub;
    private readonly ICallRepository _callRepository;
    private readonly string _webhookUrl;
    private readonly string _authToken;

    public TwilioService(
        ILogger<TwilioService> logger,
        IConfiguration configuration,
        ITranscriptHub transcriptHub,
        ICallRepository callRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        _callRepository = callRepository ?? throw new ArgumentNullException(nameof(callRepository));
        
        _webhookUrl = _configuration["Twilio:WebhookUrl"] 
            ?? throw new InvalidOperationException("Twilio:WebhookUrl configuration is required");
        
        // Initialize TwilioClient with API keys
        var keySid = _configuration["Twilio:KeySid"] 
            ?? throw new InvalidOperationException("Twilio:KeySid configuration is required");
        var keySecret = _configuration["Twilio:SecretKey"] 
            ?? throw new InvalidOperationException("Twilio:SecretKey configuration is required");
        var accountSid = _configuration["Twilio:AccountSid"] 
            ?? throw new InvalidOperationException("Twilio:AccountSid configuration is required");
        
        TwilioClient.Init(username: keySid, password: keySecret, accountSid: accountSid);
        
        // Use Auth Token for webhook validation (required - Twilio signs webhooks with Auth Token)
        _authToken = _configuration["Twilio:AuthToken"] 
            ?? throw new InvalidOperationException("Twilio:AuthToken configuration is required for webhook validation");
        
        _logger.LogInformation("TwilioClient initialized with API Key authentication, using Auth Token for webhook validation");
    }

    public async Task<string> HandleIncomingCallAsync(CallInfo callInfo, CancellationToken cancellationToken = default)
    {
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

            var response = new VoiceResponse();

            // Create a gather verb to collect user input (optional)
            // For now, we'll directly start the media stream
            
            // Start media stream for real-time audio processing
            var start = new Start();
            var stream = new Stream
            {
                Url = $"{_webhookUrl}/api/twilio/media-stream",
                Name = callInfo.CallSid // Use CallSid as stream identifier
            };
            start.Append(stream);
            response.Append(start);

            // Say a greeting message
            response.Say(
                "Thank you for calling. Your call is being processed and transcribed in real time.",
                voice: "alice",
                language: "en-US");

            // Keep the call alive
            response.Pause(length: 3600); // Max 1 hour pause

            var twiml = response.ToString();
            
            _logger.LogInformation(
                "Generated TwiML response for call: CallSid={CallSid}",
                callInfo.CallSid);

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
                "We're sorry, but we're experiencing technical difficulties. Please try again later.",
                voice: "alice",
                language: "en-US");
            errorResponse.Hangup();
            
            return errorResponse.ToString();
        }
    }

    public async ThreadingTask.Task HandleCallStatusUpdateAsync(string callSid, string status, CancellationToken cancellationToken = default)
    {
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

            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);
            throw;
        }
    }

    public bool ValidateWebhookSignature(string url, Dictionary<string, string> parameters, string signature)
    {
        try
        {
            // Check if validation is disabled (for debugging only - remove in production!)
            var disableValidation = _configuration.GetValue<bool>("Twilio:DisableWebhookValidation", false);
            if (disableValidation)
            {
                _logger.LogWarning("Webhook signature validation is DISABLED - this should only be used for debugging!");
                return true;
            }

            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("Webhook signature validation failed: signature is empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_authToken))
            {
                _logger.LogError("Webhook signature validation failed: Auth Token is not configured");
                return false;
            }

            // Try the provided URL first
            var validator = new RequestValidator(_authToken);
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

    public string GetWebhookBaseUrl()
    {
        return _webhookUrl;
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

