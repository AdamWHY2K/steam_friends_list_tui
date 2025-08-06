using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

public class FriendsListBuilder
{
    private readonly AppState _appState;

    public FriendsListBuilder(AppState appState)
    {
        _appState = appState;
    }

    public List<FriendInfo> BuildFriendsList(SteamFriends steamFriends)
    {
        var friendsList = new List<FriendInfo>();

        SteamFriendsIterator.ForEachFriendOfType(steamFriends, EFriendRelationship.Friend, steamIdFriend =>
        {
            var friendInfo = CreateFriendInfo(steamFriends, steamIdFriend);
            if (friendInfo != null)
            {
                friendsList.Add(friendInfo);
            }
        });

        return friendsList;
    }

    public List<FriendInfo> SortFriends(List<FriendInfo> friendsList)
    {
        return friendsList.OrderBy(f => string.IsNullOrEmpty(f.GameText) ? 1 : 0)
                         .ThenBy(f => f.GameText)
                         .ThenBy(f => PersonaStateHelper.GetStatusSortOrder(f.State))
                         .ThenByDescending(f => f.LastSeen)
                         .ToList();
    }

    public (int actual, int blocked, int pending) CountRelationships(SteamFriends steamFriends)
    {
        var counts = SteamFriendsIterator.CountFriendsByRelationship(steamFriends);

        int actualFriendCount = counts.GetValueOrDefault(EFriendRelationship.Friend, 0);
        int blockedCount = counts.GetValueOrDefault(EFriendRelationship.Blocked, 0) +
                          counts.GetValueOrDefault(EFriendRelationship.Ignored, 0);
        int pendingCount = counts.GetValueOrDefault(EFriendRelationship.RequestRecipient, 0) +
                          counts.GetValueOrDefault(EFriendRelationship.RequestInitiator, 0);

        return (actualFriendCount, blockedCount, pendingCount);
    }

    private FriendInfo? CreateFriendInfo(SteamFriends steamFriends, SteamID steamIdFriend)
    {
        string? friendName = steamFriends.GetFriendPersonaName(steamIdFriend);
        Console.WriteLine($"Creating friend info for {steamIdFriend}: Name='{friendName}'");

        // Sometimes the name might be empty initially but become available later
        // Let's be more permissive and create a friend info anyway
        if (string.IsNullOrEmpty(friendName))
        {
            friendName = $"Friend {steamIdFriend.AccountID}"; // Use account ID as fallback
            Console.WriteLine($"Using fallback name for {steamIdFriend}: {friendName}");
        }

        EPersonaState friendState = GetFriendState(steamFriends, steamIdFriend);
        Console.WriteLine($"Friend {friendName} ({steamIdFriend}) has state: {friendState}");

        string baseStatus = PersonaStateHelper.GetPersonaStateText(friendState);
        string statusText;
        string gameText = "";

        if (friendState == EPersonaState.Offline)
        {
            statusText = PersonaStateHelper.GetCompleteStatusText(friendState,
                _appState.TryGetLastSeenTime(steamIdFriend, out DateTime lastSeenValue) ? lastSeenValue : DateTime.MinValue);
        }
        else
        {
            // Handle online status with game information
            string? gameName = steamFriends.GetFriendGamePlayedName(steamIdFriend);
            if (!string.IsNullOrEmpty(gameName))
            {
                statusText = $"{baseStatus} - {gameName}";
                gameText = gameName;
            }
            else
            {
                var gameId = steamFriends.GetFriendGamePlayed(steamIdFriend);
                if (gameId != null && gameId.AppID != 0)
                {
                    if (_appState.TryGetAppName(gameId.AppID, out string? cachedName) && !string.IsNullOrEmpty(cachedName))
                    {
                        statusText = $"{baseStatus} - {cachedName}";
                        gameText = cachedName;
                    }
                    else
                    {
                        statusText = $"{baseStatus} - {AppConstants.LoadingText.GameName}";
                        gameText = AppConstants.LoadingText.Generic;
                    }
                }
                else
                {
                    statusText = baseStatus;
                    gameText = "";
                }
            }
        }

        DateTime lastSeenTime = _appState.TryGetLastSeenTime(steamIdFriend, out DateTime lastSeen)
            ? lastSeen
            : DateTime.MinValue;

        return new FriendInfo(steamIdFriend, friendName, friendState, statusText, gameText, lastSeenTime);
    }

    private FriendInfo CreateLoadingFriendInfo(SteamID steamIdFriend)
    {
        return new FriendInfo(
            steamIdFriend,
            AppConstants.LoadingText.Generic,
            EPersonaState.Offline,
            AppConstants.LoadingText.Generic,
            "",
            DateTime.MinValue
        );
    }

    private EPersonaState GetFriendState(SteamFriends steamFriends, SteamID steamIdFriend)
    {
        if (_appState.TryGetPersonaState(steamIdFriend, out EPersonaState trackedState))
        {
            return trackedState;
        }
        return steamFriends.GetFriendPersonaState(steamIdFriend);
    }

    public bool NeedsAppInfoRequest(SteamFriends steamFriends, SteamID steamIdFriend, out uint appId)
    {
        appId = 0;
        var gameId = steamFriends.GetFriendGamePlayed(steamIdFriend);
        if (gameId != null && gameId.AppID != 0)
        {
            if (!_appState.TryGetAppName(gameId.AppID, out string? cachedName) || string.IsNullOrEmpty(cachedName))
            {
                appId = gameId.AppID;
                return true;
            }
        }
        return false;
    }
}
