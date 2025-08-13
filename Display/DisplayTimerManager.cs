using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

/// <summary>
/// Manages the timer-based refresh logic for the display
/// </summary>
public class DisplayTimerManager : IDisposable
{
    private readonly AppState _appState;
    private readonly ILogger _logger;
    private readonly Timer _refreshTimer;
    private readonly Func<List<FriendInfo>> _getCurrentFriends;
    private readonly Action _refreshDisplay;
    private bool _disposed = false;

    public DisplayTimerManager(
        AppState appState,
        ILogger logger,
        Func<List<FriendInfo>> getCurrentFriends,
        Action refreshDisplay)
    {
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getCurrentFriends = getCurrentFriends ?? throw new ArgumentNullException(nameof(getCurrentFriends));
        _refreshDisplay = refreshDisplay ?? throw new ArgumentNullException(nameof(refreshDisplay));

        // Set up the refresh timer for updating "last seen" times
        _refreshTimer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var friends = _getCurrentFriends();
            bool hasOfflineFriends = friends.Any(f => f.State == EPersonaState.Offline && f.LastSeen != DateTime.MinValue);
            bool isDisconnected = !_appState.IsConnected;

            if (hasOfflineFriends || isDisconnected)
            {
                if (hasOfflineFriends)
                {
                    _logger.LogDebug("Timer tick: Found offline friends, refreshing display to update last seen times");

                    // Log some example friends and their times for debugging
                    var offlineFriendsWithTimes = friends.Where(f => f.State == EPersonaState.Offline && f.LastSeen != DateTime.MinValue).Take(3);
                    foreach (var friend in offlineFriendsWithTimes)
                    {
                        var timeDiff = DateTime.UtcNow - friend.LastSeen;
                        _logger.LogDebug($"Friend {friend.Name}: Last seen {friend.LastSeen:yyyy-MM-dd HH:mm:ss} UTC, diff: {timeDiff.TotalMinutes:F1} minutes");
                    }
                }

                if (isDisconnected)
                {
                    var timeText = _appState.GetTimeSinceDisconnection()?.TotalSeconds.ToString("F0") ?? "unknown";
                    _logger.LogDebug($"Timer tick: Currently disconnected for {timeText}s, refreshing display to update disconnection time");
                }

                _refreshDisplay();
            }
            else
            {
                _logger.LogDebug("Timer tick: No offline friends with times, skipping display refresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in timer tick: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _refreshTimer?.Dispose();
        _disposed = true;
    }
}
