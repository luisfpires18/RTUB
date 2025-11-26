using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using RTUB.Application.DTOs;
using System.Collections.Concurrent;

namespace RTUB.Web.Services;

/// <summary>
/// Client service for connecting to MessagesHub and handling real-time messaging events
/// </summary>
public class MessagesHubClient : IAsyncDisposable
{
    /// <summary>
    /// Reconnection delay strategy: immediate, 2s, 5s, 10s
    /// These values follow SignalR best practices for reconnection exponential backoff.
    /// Hardcoded as they are standard for all SignalR connections and rarely need customization.
    /// </summary>
    private static readonly TimeSpan[] ReconnectionDelays =
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    };

    private readonly NavigationManager _navigationManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MessagesHubClient> _logger;
    private HubConnection? _hubConnection;
    private readonly ConcurrentDictionary<int, bool> _joinedConversations = new();
    private bool _isInitialized;

    // Events for UI components to subscribe to
    public event Func<MessageDto, Task>? OnMessageReceived;
    public event Func<int, string, DateTime, Task>? OnMessageSeen;
    public event Func<int, string, Task>? OnTypingStarted;
    public event Func<int, string, Task>? OnTypingStopped;

    public MessagesHubClient(
        NavigationManager navigationManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MessagesHubClient> logger)
    {
        _navigationManager = navigationManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the hub connection is connected
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Initializes the SignalR connection
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            var hubUrl = _navigationManager.ToAbsoluteUri("/hubs/messages");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // In Blazor Server InteractiveServer mode, configure the connection to pass cookies
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext != null)
                    {
                        // Get the authentication cookie from the current HTTP context
                        var cookies = httpContext.Request.Headers.Cookie.ToString();
                        if (!string.IsNullOrEmpty(cookies))
                        {
                            options.Headers["Cookie"] = cookies;
                        }
                    }

                    options.UseDefaultCredentials = true;
                })
                .WithAutomaticReconnect(ReconnectionDelays)
                .Build();

            // Register server-to-client handlers
            _hubConnection.On<MessageDto>("ReceiveMessage", async (message) =>
            {
                _logger.LogDebug("Received message {MessageId} in conversation {ConversationId}",
                    message.Id, message.ConversationId);

                if (OnMessageReceived != null)
                {
                    await OnMessageReceived.Invoke(message);
                }
            });

            _hubConnection.On<int, string, DateTime>("MessageSeen", async (conversationId, userId, seenAt) =>
            {
                _logger.LogDebug("Messages seen in conversation {ConversationId} by user {UserId}",
                    conversationId, userId);

                if (OnMessageSeen != null)
                {
                    await OnMessageSeen.Invoke(conversationId, userId, seenAt);
                }
            });

            _hubConnection.On<int, string>("TypingStarted", async (conversationId, userId) =>
            {
                _logger.LogDebug("User {UserId} started typing in conversation {ConversationId}",
                    userId, conversationId);

                if (OnTypingStarted != null)
                {
                    await OnTypingStarted.Invoke(conversationId, userId);
                }
            });

            _hubConnection.On<int, string>("TypingStopped", async (conversationId, userId) =>
            {
                _logger.LogDebug("User {UserId} stopped typing in conversation {ConversationId}",
                    userId, conversationId);

                if (OnTypingStopped != null)
                {
                    await OnTypingStopped.Invoke(conversationId, userId);
                }
            });

            // Handle reconnection
            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "SignalR connection lost, attempting to reconnect...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async connectionId =>
            {
                _logger.LogInformation("SignalR reconnected with connection ID {ConnectionId}", connectionId);

                // Rejoin all previously joined conversations
                foreach (var conversationId in _joinedConversations.Keys)
                {
                    try
                    {
                        await _hubConnection.SendAsync("JoinConversation", conversationId);
                        _logger.LogDebug("Rejoined conversation {ConversationId} after reconnection", conversationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to rejoin conversation {ConversationId} after reconnection", conversationId);
                    }
                }
            };

            _hubConnection.Closed += error =>
            {
                if (error != null)
                {
                    _logger.LogError(error, "SignalR connection closed with error");
                }
                else
                {
                    _logger.LogInformation("SignalR connection closed");
                }
                return Task.CompletedTask;
            };

            // Start the connection
            await _hubConnection.StartAsync();
            _isInitialized = true;

            _logger.LogInformation("MessagesHub connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MessagesHub connection");
            throw;
        }
    }

    /// <summary>
    /// Joins a conversation to receive real-time updates
    /// </summary>
    public async Task JoinConversationAsync(int conversationId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join conversation {ConversationId}: hub not connected", conversationId);
            return;
        }

        if (_joinedConversations.ContainsKey(conversationId))
        {
            return; // Already joined
        }

        try
        {
            await _hubConnection.SendAsync("JoinConversation", conversationId);
            _joinedConversations[conversationId] = true;
            _logger.LogDebug("Joined conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Leaves a conversation to stop receiving real-time updates
    /// </summary>
    public async Task LeaveConversationAsync(int conversationId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        if (!_joinedConversations.ContainsKey(conversationId))
        {
            return; // Not joined
        }

        try
        {
            await _hubConnection.SendAsync("LeaveConversation", conversationId);
            _joinedConversations.TryRemove(conversationId, out _);
            _logger.LogDebug("Left conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Notifies other participants that the current user is typing
    /// </summary>
    public async Task SendTypingStartedAsync(int conversationId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.SendAsync("SendTypingStarted", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send typing started for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Notifies other participants that the current user stopped typing
    /// </summary>
    public async Task SendTypingStoppedAsync(int conversationId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.SendAsync("SendTypingStopped", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send typing stopped for conversation {ConversationId}", conversationId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            _logger.LogInformation("Disposing MessagesHub connection");
            await _hubConnection.DisposeAsync();
        }
    }
}
