using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;

namespace Prepared.Client.APIs;

/// <summary>
/// WebSocket handler for Twilio Media Streams.
/// Receives JSON events (start, media, stop) over a WebSocket connection.
/// </summary>
public class MediaStreamWebSocketHandler
{
    private readonly IMediaStreamService _mediaStreamService;
    private readonly ILogger<MediaStreamWebSocketHandler> _logger;

    public MediaStreamWebSocketHandler(
        IMediaStreamService mediaStreamService,
        ILogger<MediaStreamWebSocketHandler> logger)
    {
        _mediaStreamService = mediaStreamService ?? throw new ArgumentNullException(nameof(mediaStreamService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string? streamSid = null;
        string? callSid = null;

        try
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation(
                "Media stream WebSocket connection established. Path={Path}",
                context.Request.Path);

            var buffer = new byte[1024 * 16];

            while (true)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Media stream WebSocket close requested by Twilio.");
                    break;
                }

                var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (string.IsNullOrWhiteSpace(messageJson))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(messageJson);
                    var root = doc.RootElement;
                    var eventType = root.GetProperty("event").GetString() ?? string.Empty;

                    switch (eventType.ToLowerInvariant())
                    {
                        case "start":
                            streamSid = root.GetProperty("start").GetProperty("streamSid").GetString();
                            callSid = root.GetProperty("start").GetProperty("callSid").GetString();

                            _logger.LogInformation(
                                "Media stream start event received. StreamSid={StreamSid}, CallSid={CallSid}",
                                streamSid, callSid);

                            if (!string.IsNullOrEmpty(streamSid) && !string.IsNullOrEmpty(callSid))
                            {
                                await _mediaStreamService.HandleStreamStartAsync(streamSid, callSid);
                            }
                            break;

                        case "media":
                            // For media events we expect base64-encoded payload
                            if (root.TryGetProperty("media", out var mediaElement) &&
                                mediaElement.TryGetProperty("payload", out var payloadElement))
                            {
                                var payload = payloadElement.GetString();
                                if (!string.IsNullOrEmpty(streamSid))
                                {
                                    await _mediaStreamService.ProcessMediaDataAsync(
                                        streamSid,
                                        payload,
                                        "media");
                                }
                            }
                            break;

                        case "stop":
                            _logger.LogInformation(
                                "Media stream stop event received. StreamSid={StreamSid}, CallSid={CallSid}",
                                streamSid, callSid);

                            if (!string.IsNullOrEmpty(streamSid) && !string.IsNullOrEmpty(callSid))
                            {
                                await _mediaStreamService.HandleStreamStopAsync(streamSid, callSid);
                            }
                            break;

                        default:
                            _logger.LogWarning("Unknown media stream event type: {EventType}", eventType);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing media stream WebSocket message: {Message}", messageJson);
                }
            }

            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Closing",
                CancellationToken.None);

            _logger.LogInformation(
                "Media stream WebSocket connection closed. StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling media stream WebSocket connection. StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);
        }
    }
}


