using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Common.Enums;
using Prepared.Common.Models;
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
    private readonly string _webhookUrl;
    private readonly string _authToken;

    public TwilioService(
        ILogger<TwilioService> logger,
        IConfiguration configuration,
        ITranscriptHub transcriptHub)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        
        _webhookUrl = _configuration["Twilio:WebhookUrl"] 
            ?? throw new InvalidOperationException("Twilio:WebhookUrl configuration is required");
        
        _authToken = _configuration["Twilio:AuthToken"] 
            ?? throw new InvalidOperationException("Twilio:AuthToken configuration is required");
    }

    public async Task<string> HandleIncomingCallAsync(CallInfo callInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing incoming call: CallSid={CallSid}, From={From}, To={To}",
                callInfo.CallSid, callInfo.From, callInfo.To);

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
            
            // Notify connected clients via SignalR
            await _transcriptHub.BroadcastCallStatusUpdateAsync(
                callSid,
                status,
                cancellationToken);
            
            // Here you would typically:
            // 1. Update the call record in the database
            // 2. Trigger any cleanup or post-processing
            
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
            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("Webhook signature validation failed: signature is empty");
                return false;
            }

            // Use Twilio's request validator from Twilio.Security
            var validator = new RequestValidator(_authToken);
            var isValid = validator.Validate(url, parameters, signature);

            if (!isValid)
            {
                _logger.LogWarning(
                    "Webhook signature validation failed for URL: {Url}",
                    url);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook signature");
            return false;
        }
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

