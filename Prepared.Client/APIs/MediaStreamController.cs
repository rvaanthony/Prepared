using Microsoft.AspNetCore.Mvc;
using Prepared.Business.Interfaces;

namespace Prepared.Client.APIs;

/// <summary>
/// Processes Twilio Media Stream webhook events.
/// </summary>
[ApiController]
[Route("api/twilio/media-stream")]
public class MediaStreamController : ControllerBase
{
    private readonly IMediaStreamService _mediaStreamService;
    private readonly ILogger<MediaStreamController> _logger;

    public MediaStreamController(
        IMediaStreamService mediaStreamService,
        ILogger<MediaStreamController> logger)
    {
        _mediaStreamService = mediaStreamService ?? throw new ArgumentNullException(nameof(mediaStreamService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles Media Stream webhooks from Twilio (start, media, stop events)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleMediaStream()
    {
        try
        {
            _logger.LogInformation(
                "Media stream webhook received. Method: {Method}, Path: {Path}, HasForm: {HasForm}, FormKeys: {FormKeys}",
                Request.Method, Request.Path, Request.HasFormContentType, 
                Request.HasFormContentType ? string.Join(", ", Request.Form.Keys) : "none");

            var streamSid = Request.Form["StreamSid"].ToString();
            var callSid = Request.Form["CallSid"].ToString();
            var eventType = Request.Form["Event"].ToString();
            var mediaPayload = Request.Form["MediaPayload"].ToString();

            _logger.LogInformation(
                "Received media stream event: StreamSid={StreamSid}, CallSid={CallSid}, Event={Event}, HasPayload={HasPayload}",
                streamSid, callSid, eventType, !string.IsNullOrEmpty(mediaPayload));

            switch (eventType?.ToLowerInvariant())
            {
                case "start":
                    await _mediaStreamService.HandleStreamStartAsync(streamSid, callSid);
                    break;

                case "media":
                    await _mediaStreamService.ProcessMediaDataAsync(streamSid, mediaPayload, eventType);
                    break;

                case "stop":
                    await _mediaStreamService.HandleStreamStopAsync(streamSid, callSid);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown media stream event type: StreamSid={StreamSid}, Event={Event}",
                        streamSid, eventType);
                    break;
            }

            // Twilio expects a 200 OK response
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing media stream webhook");
            // Still return 200 to prevent Twilio from retrying
            return Ok();
        }
    }
}

