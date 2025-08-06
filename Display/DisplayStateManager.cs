using SteamKit2;
using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;

namespace SteamFriendsTUI.Display;

/// <summary>
/// Manages the state and data for the display system
/// </summary>
public class DisplayStateManager
{
    private readonly AppState _appState;
    private readonly FriendsListBuilder _friendsBuilder;
    private readonly ILogger _logger;
    private readonly object _stateLock = new();

    private List<FriendInfo> _currentFriends = new();
    private (int friends, int blocked, int pending) _currentCounts = (0, 0, 0);

    public event Action<uint>? AppInfoRequested;

    public DisplayStateManager(AppState appState, ILogger logger)
    {
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
        _friendsBuilder = new FriendsListBuilder(appState);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Updates the friends list from Steam data
    /// </summary>
    public void UpdateFromSteam(SteamFriends? steamFriends)
    {
        if (steamFriends == null)
        {
            _logger.LogWarning("UpdateFromSteam called with null steamFriends");
            return;
        }

        try
        {
            var counts = _friendsBuilder.CountRelationships(steamFriends);
            var friends = BuildFriendsList(steamFriends, counts);

            lock (_stateLock)
            {
                _currentCounts = counts;
                _currentFriends = friends;
            }

            ProcessAppInfoRequests(steamFriends);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating state from Steam", ex);
            SetErrorState();
        }
    }

    /// <summary>
    /// Gets the current friends list (thread-safe)
    /// </summary>
    public List<FriendInfo> GetCurrentFriends()
    {
        lock (_stateLock)
        {
            return _currentFriends.ToList();
        }
    }

    /// <summary>
    /// Gets the current counts (thread-safe)
    /// </summary>
    public (int friends, int blocked, int pending) GetCurrentCounts()
    {
        lock (_stateLock)
        {
            return _currentCounts;
        }
    }

    private List<FriendInfo> BuildFriendsList(SteamFriends steamFriends, (int friends, int blocked, int pending) counts)
    {
        if (counts.friends == 0)
        {
            return CreateNoFriendsState();
        }

        var friendsList = _friendsBuilder.BuildFriendsList(steamFriends);
        _logger.LogDebug($"Built friends list with {friendsList.Count} friends");

        var sortedFriends = _friendsBuilder.SortFriends(friendsList);
        _logger.LogDebug($"Sorted friends list with {sortedFriends.Count} friends");

        return sortedFriends;
    }

    private List<FriendInfo> CreateNoFriendsState()
    {
        return new List<FriendInfo>
        {
            new FriendInfo(
                new SteamID(0),
                AppConstants.LoadingText.NoFriendsFound,
                EPersonaState.Offline,
                "",
                "",
                DateTime.MinValue
            )
        };
    }

    private void SetErrorState()
    {
        lock (_stateLock)
        {
            _currentFriends = new List<FriendInfo>
            {
                new FriendInfo(
                    new SteamID(0),
                    "Error loading friends list",
                    EPersonaState.Offline,
                    "",
                    "",
                    DateTime.MinValue
                )
            };
        }
    }

    private void ProcessAppInfoRequests(SteamFriends steamFriends)
    {
        try
        {
            SteamFriendsIterator.ForEachFriendOfType(steamFriends, EFriendRelationship.Friend, steamIdFriend =>
            {
                if (_friendsBuilder.NeedsAppInfoRequest(steamFriends, steamIdFriend, out uint appId))
                {
                    AppInfoRequested?.Invoke(appId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing app info requests", ex);
        }
    }
}
