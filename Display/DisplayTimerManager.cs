using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

/// <summary>
/// Manages the timer-based refresh logic for the display, only refreshing when content actually changes
/// </summary>
public class DisplayTimerManager : IDisposable
{
    private readonly AppState _appState;
    private readonly ILogger _logger;
    private readonly Timer _refreshTimer;
    private readonly Func<List<FriendInfo>> _getCurrentFriends;
    private readonly Action _refreshDisplay;
    private bool _disposed = false;

    // Cache the last displayed state to detect changes
    private readonly object _stateLock = new();
    private string? _lastDisconnectionText = null;
    private readonly Dictionary<SteamID, string> _lastFriendStatusTexts = new();

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

        // Set up the refresh timer - only refresh if something changed
        _refreshTimer = new Timer(OnTimerTick, null, AppConstants.Timeouts.TimerRefresh, AppConstants.Timeouts.TimerRefresh);
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            bool needsRefresh = false;
            var changeReasons = new List<string>();

            lock (_stateLock)
            {
                // Check if disconnection status text has changed
                if (_appState.IsConnected == false)
                {
                    var currentDisconnectionText = GetCurrentDisconnectionText();
                    if (_lastDisconnectionText != currentDisconnectionText)
                    {
                        _lastDisconnectionText = currentDisconnectionText;
                        needsRefresh = true;
                        changeReasons.Add($"disconnection status changed to '{currentDisconnectionText}'");
                    }
                }
                else if (_lastDisconnectionText != null)
                {
                    // We were disconnected but now we're connected
                    _lastDisconnectionText = null;
                    needsRefresh = true;
                    changeReasons.Add("reconnected to Steam");
                }

                // Check if any offline friend status text has changed
                var friends = _getCurrentFriends();
                var offlineFriends = friends.Where(f => f.State == EPersonaState.Offline && f.LastSeen != DateTime.MinValue);

                foreach (var friend in offlineFriends)
                {
                    var currentStatusText = PersonaStateHelper.GetFormattedLastSeenText(friend.LastSeen);

                    if (_lastFriendStatusTexts.TryGetValue(friend.SteamId, out var lastStatusText))
                    {
                        if (lastStatusText != currentStatusText)
                        {
                            _lastFriendStatusTexts[friend.SteamId] = currentStatusText;
                            needsRefresh = true;
                            changeReasons.Add($"friend {friend.Name} status changed from '{lastStatusText}' to '{currentStatusText}'");
                        }
                    }
                    else
                    {
                        // First time seeing this offline friend
                        _lastFriendStatusTexts[friend.SteamId] = currentStatusText;
                        needsRefresh = true;
                        changeReasons.Add($"new offline friend {friend.Name} with status '{currentStatusText}'");
                    }
                }

                // Remove friends that are no longer offline from our tracking
                var currentOfflineFriendIds = offlineFriends.Select(f => f.SteamId).ToHashSet();
                var keysToRemove = _lastFriendStatusTexts.Keys.Where(id => !currentOfflineFriendIds.Contains(id)).ToList();
                foreach (var key in keysToRemove)
                {
                    _lastFriendStatusTexts.Remove(key);
                }
            }

            if (needsRefresh)
            {
                var reasonText = string.Join(", ", changeReasons);
                _logger.LogDebug($"Timer refresh triggered: {reasonText}");
                _refreshDisplay();
            }
            else
            {
                _logger.LogDebug("Timer tick: No display changes detected, skipping refresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in timer tick: {ex.Message}");
        }
    }

    private string GetCurrentDisconnectionText()
    {
        var timeSinceDisconnection = _appState.GetTimeSinceDisconnection();
        if (!timeSinceDisconnection.HasValue)
            return "Steam Disconnected";

        return $"Disconnected {PersonaStateHelper.GetLastSeenText(timeSinceDisconnection.Value)}";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _refreshTimer?.Dispose();
        lock (_stateLock)
        {
            _lastFriendStatusTexts.Clear();
        }
        _disposed = true;
    }
}
