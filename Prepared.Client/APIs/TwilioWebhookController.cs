using Microsoft.AspNetCore.Mvc;
using Prepared.Business.Interfaces;
using Prepared.Common.Models;
using Prepared.Common.Enums;
using Twilio.AspNet.Core;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace Prepared.Client.APIs;

/// <summary>
/// Controller for handling Twilio webhooks with production-ready security and error handling
/// </summary>
[ApiController]
[Route("api/twilio")]
public class TwilioWebhookController : TwilioController
{
    private readonly ITwilioService _twilioService;
    private readonly ILogger<TwilioWebhookController> _logger;

    public TwilioWebhookController(
        ITwilioService twilioService,
        ILogger<TwilioWebhookController> logger)
    {
        _twilioService = twilioService ?? throw new ArgumentNullException(nameof(twilioService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles incoming call webhooks from Twilio
    /// </summary>
    [HttpPost("incoming-call")]
    public async Task<IActionResult> HandleIncomingCall()
    {
        try
        {
            // Validate webhook signature for security
            // Check for signature header (case-insensitive in ASP.NET Core)
            if (!Request.Headers.TryGetValue("X-Twilio-Signature", out var signatureHeader) || 
                string.IsNullOrWhiteSpace(signatureHeader.ToString()))
            {
                _logger.LogWarning(
                    "Incoming call webhook missing signature header. Available headers: {Headers}",
                    string.Join(", ", Request.Headers.Keys));
                return Unauthorized();
            }

            var signature = signatureHeader.ToString();
            
            // Use the configured webhook URL as the base, then append the specific endpoint path
            // This ensures we use the exact URL that Twilio has configured
            var webhookBaseUrl = _twilioService.GetWebhookBaseUrl().TrimEnd('/');
            var endpointPath = Request.Path.Value?.TrimStart('/') ?? "api/twilio/incoming-call";
            var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
            var url = $"{webhookBaseUrl}/{endpointPath}{queryString}";
            
            // Extract parameters - StringValues might have multiple values, take the first one
            var parameters = Request.Form.ToDictionary(
                f => f.Key, 
                f => f.Value.Count > 0 ? f.Value[0]! : string.Empty);

            _logger.LogInformation(
                "Validating webhook signature. Configured base URL: {BaseUrl}, Constructed URL: {Url}, Path: {Path}, QueryString: {QueryString}, Signature present: {HasSignature}",
                webhookBaseUrl, url, Request.Path, Request.QueryString, !string.IsNullOrEmpty(signature));

            if (!_twilioService.ValidateWebhookSignature(url, parameters, signature))
            {
                _logger.LogWarning(
                    "Invalid webhook signature for incoming call. URL: {Url}, Parameters: {ParamCount}, Signature: {SignaturePrefix}...",
                    url, parameters?.Count ?? 0, signature?.Length > 10 ? signature.Substring(0, 10) : "empty");
                return Unauthorized();
            }

            // Extract call information from Twilio webhook
            var callInfo = new CallInfo
            {
                CallSid = Request.Form["CallSid"].ToString(),
                From = Request.Form["From"].ToString(),
                To = Request.Form["To"].ToString(),
                Direction = Request.Form["Direction"].ToString(),
                Status = MapStatus(Request.Form["CallStatus"].ToString()),
                StartedAt = DateTime.UtcNow,
                AccountSid = Request.Form["AccountSid"].ToString()
            };

            _logger.LogInformation(
                "Received incoming call webhook: CallSid={CallSid}, From={From}, To={To}",
                callInfo.CallSid, callInfo.From, callInfo.To);

            // Generate TwiML response
            var twiml = await _twilioService.HandleIncomingCallAsync(callInfo);

            return Content(twiml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing incoming call webhook");
            
            // Return a safe TwiML response
            var errorResponse = new VoiceResponse();
            errorResponse.Say("We're experiencing technical difficulties. Please try again later.");
            errorResponse.Hangup();
            
            return Content(errorResponse.ToString(), "application/xml");
        }
    }

    /// <summary>
    /// Handles call status update webhooks from Twilio
    /// </summary>
    [HttpPost("call-status")]
    public async Task<IActionResult> HandleCallStatus()
    {
        try
        {
            // Validate webhook signature
            // Check for signature header (case-insensitive in ASP.NET Core)
            if (!Request.Headers.TryGetValue("X-Twilio-Signature", out var signatureHeader) || 
                string.IsNullOrWhiteSpace(signatureHeader.ToString()))
            {
                _logger.LogWarning(
                    "Call status webhook missing signature header. Available headers: {Headers}",
                    string.Join(", ", Request.Headers.Keys));
                return Unauthorized();
            }

            var signature = signatureHeader.ToString();
            
            // Use the configured webhook URL as the base, then append the specific endpoint path
            // This ensures we use the exact URL that Twilio has configured
            var webhookBaseUrl = _twilioService.GetWebhookBaseUrl().TrimEnd('/');
            var endpointPath = Request.Path.Value?.TrimStart('/') ?? "api/twilio/call-status";
            var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
            var url = $"{webhookBaseUrl}/{endpointPath}{queryString}";
            
            // Extract parameters - StringValues might have multiple values, take the first one
            var parameters = Request.Form.ToDictionary(
                f => f.Key, 
                f => f.Value.Count > 0 ? f.Value[0]! : string.Empty);

            _logger.LogInformation(
                "Validating webhook signature. Configured base URL: {BaseUrl}, Constructed URL: {Url}, Path: {Path}, QueryString: {QueryString}, Signature present: {HasSignature}",
                webhookBaseUrl, url, Request.Path, Request.QueryString, !string.IsNullOrEmpty(signature));

            if (!_twilioService.ValidateWebhookSignature(url, parameters, signature))
            {
                _logger.LogWarning(
                    "Invalid webhook signature for call status update. URL: {Url}, Parameters: {ParamCount}, Signature: {SignaturePrefix}...",
                    url, parameters?.Count ?? 0, signature?.Length > 10 ? signature.Substring(0, 10) : "empty");
                return Unauthorized();
            }

            var callSid = Request.Form["CallSid"].ToString();
            var status = Request.Form["CallStatus"].ToString();

            _logger.LogInformation(
                "Received call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);

            await _twilioService.HandleCallStatusUpdateAsync(callSid, status);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing call status webhook");
            return StatusCode(500, "Internal server error");
        }
    }

    private static CallStatus MapStatus(string twilioStatus)
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

