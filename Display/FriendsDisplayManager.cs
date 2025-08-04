using System.Text;
using SteamKit2;
using SteamFriendsCLI.Constants;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

public class FriendsDisplayManager : IFriendsDisplayManager
{
    private readonly AppState _appState;
    
    // Event for requesting app info when game names are not cached
    public event Action<uint>? AppInfoRequested;

    public FriendsDisplayManager(AppState appState)
    {
        _appState = appState;
    }

    public void DisplayFriendsList(SteamFriends? steamFriends)
    {
        if (steamFriends == null)
            return;

        var (actualFriendCount, blockedCount, pendingCount, ignoredCount) = CountRelationships(steamFriends);

        DisplayHeader(actualFriendCount, blockedCount, pendingCount, ignoredCount);
        DisplayCurrentUser();

        if (actualFriendCount == 0)
        {
            DisplayNoFriends();
            return;
        }

        Console.WriteLine("║                                                                                ║");

        var friendsList = BuildFriendsList(steamFriends);
        var sortedFriends = SortFriends(friendsList);

        DisplayFriends(sortedFriends);
        DisplayFooter();
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
                case EFriendRelationship.RequestInitiator:
                    pendingCount++;
                    break;
                case EFriendRelationship.Ignored:
                    ignoredCount++;
                    break;
            }
        }

        return (actualFriendCount, blockedCount, pendingCount, ignoredCount);
    }

    private void DisplayHeader(int friends, int blocked, int pending, int ignored)
    {
        Console.WriteLine();
        Console.WriteLine($"{DisplayConstants.BOX_TOP_LEFT}{new string(DisplayConstants.BOX_HORIZONTAL[0], DisplayConstants.MAX_DISPLAY_WIDTH)}{DisplayConstants.BOX_TOP_RIGHT}");
        Console.WriteLine($"{DisplayConstants.BOX_VERTICAL}                                 STEAM FRIENDS LIST                                {DisplayConstants.BOX_VERTICAL}");
        Console.WriteLine($"{DisplayConstants.BOX_T_DOWN}{new string(DisplayConstants.BOX_HORIZONTAL[0], DisplayConstants.MAX_DISPLAY_WIDTH)}{DisplayConstants.BOX_T_UP}");
        Console.WriteLine($"{DisplayConstants.BOX_VERTICAL} {friends} friends, {pending} pending, {blocked} blocked, {ignored} ignored{"",-34} {DisplayConstants.BOX_VERTICAL}");
        Console.WriteLine($"{DisplayConstants.BOX_T_DOWN}{new string(DisplayConstants.BOX_HORIZONTAL[0], DisplayConstants.MAX_DISPLAY_WIDTH)}{DisplayConstants.BOX_T_UP}");
    }

    private void DisplayCurrentUser()
    {
        string baseUserStatus = PersonaStateHelper.GetPersonaStateText(_appState.CurrentUserState);
        string currentUserStatus = string.IsNullOrEmpty(_appState.CurrentGame) 
            ? baseUserStatus 
            : $"{baseUserStatus} - {_appState.CurrentGame}";
        string currentUserColor = PersonaStateHelper.GetPersonaStateColor(_appState.CurrentUserState);

        string displayPersonaName = TruncateString(_appState.CurrentPersonaName, 70);
        
        string currentUserNameLine = CreateNameLine($"{displayPersonaName} (You)");
        string currentUserStatusLine = CreateStatusLine(currentUserColor, currentUserStatus);

        Console.WriteLine(currentUserNameLine);
        Console.WriteLine(currentUserStatusLine);
        Console.WriteLine("║                                                                                ║");
    }

    private void DisplayNoFriends()
    {
        Console.WriteLine("║                              No friends found                                 ║");
        DisplayFooter();
    }

    private void DisplayFooter()
    {
        Console.WriteLine($"{DisplayConstants.BOX_BOTTOM_LEFT}{new string(DisplayConstants.BOX_HORIZONTAL[0], DisplayConstants.MAX_DISPLAY_WIDTH)}{DisplayConstants.BOX_BOTTOM_RIGHT}");
        Console.WriteLine();
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
                // Request app info for this game
                AppInfoRequested?.Invoke(gameId.AppID);
                return ($"{baseStatus} - Loading game name...", "Loading...");
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

    private void DisplayFriends(List<FriendInfo> sortedFriends)
    {
        foreach (var friend in sortedFriends)
        {
            string friendName = TruncateString(friend.Name, DisplayConstants.MAX_NAME_LENGTH);
            string statusText = TruncateString(friend.StatusText, DisplayConstants.MAX_STATUS_LENGTH);

            string nameLine = CreateNameLine(friendName);
            string statusLine = CreateStatusLine(friend.StatusColor, statusText);

            Console.WriteLine(nameLine);
            Console.WriteLine(statusLine);
            Console.WriteLine("║                                                                                ║");
        }
    }

    private string TruncateString(string input, int maxLength)
    {
        if (input.Length <= maxLength)
            return input;
        
        return input.Substring(0, maxLength - DisplayConstants.TRUNCATION_SUFFIX.Length) + DisplayConstants.TRUNCATION_SUFFIX;
    }

    private string CreateNameLine(string name)
    {
        return $"║ {name}".PadRight(DisplayConstants.NAME_LINE_TARGET_LENGTH) + " ║";
    }

    private string CreateStatusLine(string color, string status)
    {
        string line = $"║{DisplayConstants.STATUS_INDENT}{color}{status}{DisplayConstants.Colors.RESET}";
        return line.PadRight(DisplayConstants.STATUS_LINE_TARGET_LENGTH) + " ║";
    }

    public void UpdateConnectionStatus(string status)
    {
        Console.WriteLine(status);
    }

    public void Initialize()
    {
        // No initialization needed for console interface
    }

    public void Run()
    {
        // Console interface doesn't need a separate run loop
    }

    public void Stop()
    {
        // No cleanup needed for console interface
    }

    public void Dispose()
    {
        // No resources to dispose for console interface
    }
}
