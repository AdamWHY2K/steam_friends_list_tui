using System.Text;
using Terminal.Gui;
using SteamKit2;
using SteamFriendsCLI.Constants;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

public class TerminalGuiDisplayManager : IDisposable
{
    private readonly AppState _appState;
    private Window? _mainWindow;
    private ListView? _friendsListView;
    private Label? _statusLabel;
    private Label? _userLabel;
    private List<FriendInfo> _currentFriendsList = new();
    private bool _isInitialized = false;

    public TerminalGuiDisplayManager(AppState appState)
    {
        _appState = appState;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        Application.Init();
        Colors.Base.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);
        Colors.Base.Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black);
        Colors.Base.HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black);
        Colors.Base.HotFocus = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black);
        Colors.Base.Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black);

        _mainWindow = new Window("Steam Friends CLI")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        CreateUserInfoSection();
        CreateFriendsListSection();
        CreateStatusSection();

        Application.Top.Add(_mainWindow);
        _isInitialized = true;
    }

    private void CreateUserInfoSection()
    {
        _userLabel = new Label("Loading user info...")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = 1
        };
        _mainWindow!.Add(_userLabel);
    }

    private void CreateFriendsListSection()
    {
        var friendsFrame = new FrameView("Friends")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 6
        };

        _friendsListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            AllowsMultipleSelection = false
        };

        friendsFrame.Add(_friendsListView);
        _mainWindow!.Add(friendsFrame);
    }

    private void CreateStatusSection()
    {
        _statusLabel = new Label("Connecting to Steam...")
        {
            X = 1,
            Y = Pos.Bottom(_mainWindow!) - 3,
            Width = Dim.Fill() - 2,
            Height = 1
        };
        _mainWindow!.Add(_statusLabel);

        var helpLabel = new Label("Press 'q' to quit, 'r' to refresh")
        {
            X = 1,
            Y = Pos.Bottom(_mainWindow!) - 2,
            Width = Dim.Fill() - 2,
            Height = 1
        };
        _mainWindow!.Add(helpLabel);
    }

    public void UpdateFriendsList(SteamFriends? steamFriends)
    {
        if (!_isInitialized || steamFriends == null)
            return;

        Application.MainLoop.Invoke(() =>
        {
            var (actualFriendCount, blockedCount, pendingCount, ignoredCount) = CountRelationships(steamFriends);
            
            UpdateStatusLabel(actualFriendCount, blockedCount, pendingCount, ignoredCount);
            UpdateUserInfo();

            if (actualFriendCount == 0)
            {
                _friendsListView!.SetSource(new string[] { "No friends found" });
                return;
            }

            var friendsList = BuildFriendsList(steamFriends);
            var sortedFriends = SortFriends(friendsList);
            _currentFriendsList = sortedFriends;

            var displayStrings = sortedFriends.Select(FormatFriendForDisplay).ToArray();
            _friendsListView!.SetSource(displayStrings);
        });
    }

    public void DisplayFriendsList(SteamFriends? steamFriends)
    {
        UpdateFriendsList(steamFriends);
    }

    private string FormatFriendForDisplay(FriendInfo friend)
    {
        var nameAndStatus = $"{friend.Name}";
        
        if (!string.IsNullOrEmpty(friend.GameText))
        {
            nameAndStatus += $" - Playing: {friend.GameText}";
        }
        else
        {
            var stateText = PersonaStateHelper.GetPersonaStateText(friend.State);
            if (friend.State == EPersonaState.Offline && friend.LastSeen != DateTime.MinValue)
            {
                var timeDiff = DateTime.Now - friend.LastSeen;
                var lastSeenText = PersonaStateHelper.GetLastSeenText(timeDiff);
                nameAndStatus += $" - {stateText} (Last online {lastSeenText})";
            }
            else
            {
                nameAndStatus += $" - {stateText}";
            }
        }

        return nameAndStatus;
    }

    private void UpdateStatusLabel(int actualFriendCount, int blockedCount, int pendingCount, int ignoredCount)
    {
        var status = $"Friends: {actualFriendCount}";
        if (blockedCount > 0 || pendingCount > 0 || ignoredCount > 0)
        {
            status += $" | Blocked: {blockedCount} | Pending: {pendingCount} | Ignored: {ignoredCount}";
        }
        _statusLabel!.Text = status;
    }

    private void UpdateUserInfo()
    {
        var userInfo = $"User: {_appState.CurrentPersonaName ?? "Loading..."}";
        if (!string.IsNullOrEmpty(_appState.CurrentGame))
        {
            userInfo += $" - Currently playing: {_appState.CurrentGame}";
        }
        else
        {
            var stateText = PersonaStateHelper.GetPersonaStateText(_appState.CurrentUserState);
            userInfo += $" - {stateText}";
        }
        _userLabel!.Text = userInfo;
    }

    private (int actual, int blocked, int pending, int ignored) CountRelationships(SteamFriends steamFriends)
    {
        int totalCount = steamFriends.GetFriendCount();
        int actualFriendCount = 0, blockedCount = 0, pendingCount = 0, ignoredCount = 0;

        for (int i = 0; i < totalCount; i++)
        {
            SteamID steamIdFriend = steamFriends.GetFriendByIndex(i);
            EFriendRelationship relationship = steamFriends.GetFriendRelationship(steamIdFriend);

            switch (relationship)
            {
                case EFriendRelationship.Friend:
                    actualFriendCount++;
                    break;
                case EFriendRelationship.Blocked:
                    blockedCount++;
                    break;
                case EFriendRelationship.RequestRecipient:
                    pendingCount++;
                    break;
                case EFriendRelationship.Ignored:
                    ignoredCount++;
                    break;
            }
        }

        return (actualFriendCount, blockedCount, pendingCount, ignoredCount);
    }

    private List<FriendInfo> BuildFriendsList(SteamFriends steamFriends)
    {
        var friendsList = new List<FriendInfo>();
        int totalCount = steamFriends.GetFriendCount();

        for (int x = 0; x < totalCount; x++)
        {
            SteamID steamIdFriend = steamFriends.GetFriendByIndex(x);
            EFriendRelationship relationship = steamFriends.GetFriendRelationship(steamIdFriend);

            if (relationship == EFriendRelationship.Friend)
            {
                var friendInfo = CreateFriendInfo(steamFriends, steamIdFriend);
                if (friendInfo != null)
                {
                    friendsList.Add(friendInfo);
                }
            }
        }

        return friendsList;
    }

    private FriendInfo? CreateFriendInfo(SteamFriends steamFriends, SteamID steamIdFriend)
    {
        string? friendName = steamFriends.GetFriendPersonaName(steamIdFriend);
        
        if (string.IsNullOrEmpty(friendName))
        {
            return new FriendInfo(
                steamIdFriend, 
                "Loading...", 
                EPersonaState.Offline, 
                "Loading...", 
                DisplayConstants.Colors.DARK_GRAY, 
                "", 
                DateTime.MinValue
            );
        }

        EPersonaState friendState = GetFriendState(steamFriends, steamIdFriend);
        string baseStatus = PersonaStateHelper.GetPersonaStateText(friendState);
        string statusColor = PersonaStateHelper.GetPersonaStateColor(friendState);
        string statusText;
        string gameText = "";

        if (friendState == EPersonaState.Offline)
        {
            statusText = GetOfflineStatusText(steamIdFriend, baseStatus);
        }
        else
        {
            (statusText, gameText) = GetOnlineStatusText(steamFriends, steamIdFriend, baseStatus);
        }

        DateTime lastSeenTime = _appState.TryGetLastSeenTime(steamIdFriend, out DateTime lastSeen) 
            ? lastSeen 
            : DateTime.MinValue;

        return new FriendInfo(steamIdFriend, friendName, friendState, statusText, statusColor, gameText, lastSeenTime);
    }

    private EPersonaState GetFriendState(SteamFriends steamFriends, SteamID steamIdFriend)
    {
        if (_appState.TryGetPersonaState(steamIdFriend, out EPersonaState trackedState))
        {
            return trackedState;
        }
        return steamFriends.GetFriendPersonaState(steamIdFriend);
    }

    private string GetOfflineStatusText(SteamID steamIdFriend, string baseStatus)
    {
        if (_appState.TryGetLastSeenTime(steamIdFriend, out DateTime lastSeenValue) && lastSeenValue != DateTime.MinValue)
        {
            var timeDiff = DateTime.Now - lastSeenValue;
            string lastSeenText = PersonaStateHelper.GetLastSeenText(timeDiff);
            return $"{baseStatus} - Last online {lastSeenText}";
        }
        return baseStatus;
    }

    private (string statusText, string gameText) GetOnlineStatusText(SteamFriends steamFriends, SteamID steamIdFriend, string baseStatus)
    {
        string? gameName = steamFriends.GetFriendGamePlayedName(steamIdFriend);
        if (!string.IsNullOrEmpty(gameName))
        {
            return ($"{baseStatus} - {gameName}", gameName);
        }

        var gameId = steamFriends.GetFriendGamePlayed(steamIdFriend);
        if (gameId != null && gameId.AppID != 0)
        {
            if (_appState.TryGetAppName(gameId.AppID, out string? cachedName) && !string.IsNullOrEmpty(cachedName))
            {
                return ($"{baseStatus} - {cachedName}", cachedName);
            }
            else
            {
                // App info will be requested by the callback handler
                return ($"{baseStatus} - Game ID: {gameId.AppID}", $"Game ID: {gameId.AppID}");
            }
        }

        return (baseStatus, "");
    }

    private List<FriendInfo> SortFriends(List<FriendInfo> friendsList)
    {
        return friendsList.OrderBy(f => string.IsNullOrEmpty(f.GameText) ? 1 : 0)
                         .ThenBy(f => f.GameText)
                         .ThenBy(f => PersonaStateHelper.GetStatusSortOrder(f.State))
                         .ThenByDescending(f => f.LastSeen)
                         .ToList();
    }

    private void RefreshDisplay()
    {
        _statusLabel!.Text = "Refreshing...";
        // The refresh will be triggered by the callback handler
    }

    public void Run()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("TerminalGuiDisplayManager must be initialized before running");

        Application.Run();
    }

    public void Stop()
    {
        if (_isInitialized)
        {
            Application.RequestStop();
        }
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
            return;

        Application.MainLoop.Invoke(() =>
        {
            _statusLabel!.Text = status;
        });
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            Application.Shutdown();
            _isInitialized = false;
        }
    }
}
