using Microsoft.AspNetCore.Mvc;
using Prepared.Business.Interfaces;

namespace Prepared.Client.APIs;

/// <summary>
/// Controller for handling Twilio Media Stream webhooks with production-ready error handling
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
            var streamSid = Request.Form["StreamSid"].ToString();
            var callSid = Request.Form["CallSid"].ToString();
            var eventType = Request.Form["Event"].ToString();
            var mediaPayload = Request.Form["MediaPayload"].ToString();

            _logger.LogDebug(
                "Received media stream event: StreamSid={StreamSid}, CallSid={CallSid}, Event={Event}",
                streamSid, callSid, eventType);

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

