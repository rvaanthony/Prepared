using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Prepared.Client.Hubs;

/// <summary>
/// SignalR Hub for broadcasting real-time transcript updates to connected clients
/// Implements production-ready patterns with error handling, logging, and connection management
/// </summary>
public class TranscriptHub : Hub
{
    private readonly ILogger<TranscriptHub> _logger;

    public TranscriptHub(ILogger<TranscriptHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userId = Context.UserIdentifier ?? "anonymous";
            
            _logger.LogInformation(
                "Client connected to TranscriptHub: ConnectionId={ConnectionId}, UserId={UserId}",
                connectionId, userId);

            // Add connection to a group for the specific call/stream if provided
            var callSid = Context.GetHttpContext()?.Request.Query["callSid"].ToString();
            if (!string.IsNullOrEmpty(callSid))
            {
                await Groups.AddToGroupAsync(connectionId, GetCallGroup(callSid));
                _logger.LogDebug(
                    "Added connection to call group: ConnectionId={ConnectionId}, CallSid={CallSid}",
                    connectionId, callSid);
            }

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in OnConnectedAsync: ConnectionId={ConnectionId}",
                Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userId = Context.UserIdentifier ?? "anonymous";

            if (exception != null)
            {
                _logger.LogWarning(exception,
                    "Client disconnected with error: ConnectionId={ConnectionId}, UserId={UserId}",
                    connectionId, userId);
            }
            else
            {
                _logger.LogInformation(
                    "Client disconnected: ConnectionId={ConnectionId}, UserId={UserId}",
                    connectionId, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in OnDisconnectedAsync: ConnectionId={ConnectionId}",
                Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Allows clients to join a specific call's transcript group
    /// </summary>
    public async Task JoinCallGroup(string callSid)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                _logger.LogWarning(
                    "Attempted to join call group with empty CallSid: ConnectionId={ConnectionId}",
                    Context.ConnectionId);
                return;
            }

            var connectionId = Context.ConnectionId;
            var groupName = GetCallGroup(callSid);
            
            await Groups.AddToGroupAsync(connectionId, groupName);
            
            _logger.LogInformation(
                "Client joined call group: ConnectionId={ConnectionId}, CallSid={CallSid}",
                connectionId, callSid);

            // Notify the client they've successfully joined
            await Clients.Caller.SendAsync("JoinedCallGroup", callSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error joining call group: ConnectionId={ConnectionId}, CallSid={CallSid}",
                Context.ConnectionId, callSid);
            throw;
        }
    }

    /// <summary>
    /// Allows clients to leave a specific call's transcript group
    /// </summary>
    public async Task LeaveCallGroup(string callSid)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                return;
            }

            var connectionId = Context.ConnectionId;
            var groupName = GetCallGroup(callSid);
            
            await Groups.RemoveFromGroupAsync(connectionId, groupName);
            
            _logger.LogInformation(
                "Client left call group: ConnectionId={ConnectionId}, CallSid={CallSid}",
                connectionId, callSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error leaving call group: ConnectionId={ConnectionId}, CallSid={CallSid}",
                Context.ConnectionId, callSid);
            throw;
        }
    }

    /// <summary>
    /// Gets the group name for a specific call
    /// </summary>
    private static string GetCallGroup(string callSid)
    {
        return $"call_{callSid}";
    }
}

