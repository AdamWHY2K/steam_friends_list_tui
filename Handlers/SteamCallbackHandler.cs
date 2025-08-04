using SteamKit2;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Display;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Handlers;

public class SteamCallbackHandler
{
    private readonly SteamClient _steamClient;
    private readonly SteamUser _steamUser;
    private readonly SteamFriends _steamFriends;
    private readonly SteamApps _steamApps;
    private readonly AppState _appState;
    private readonly FriendsDisplayManager _displayManager;

    public SteamCallbackHandler(
        SteamClient steamClient,
        SteamUser steamUser,
        SteamFriends steamFriends,
        SteamApps steamApps,
        AppState appState,
        FriendsDisplayManager displayManager)
    {
        _steamClient = steamClient;
        _steamUser = steamUser;
        _steamFriends = steamFriends;
        _steamApps = steamApps;
        _appState = appState;
        _displayManager = displayManager;
    }

    public void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine("Disconnected from Steam");
        _appState.IsRunning = false;
    }

    public void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);
            _appState.IsRunning = false;
            return;
        }
        Console.WriteLine("Successfully logged on!");
    }

    public void OnAccountInfo(SteamUser.AccountInfoCallback callback)
    {
        _appState.CurrentPersonaName = callback.PersonaName;
        _appState.CurrentGame = "";

        _steamFriends.SetPersonaState(EPersonaState.Online);
        _appState.CurrentUserState = EPersonaState.Online;
    }

    public void OnPlayingSession(SteamUser.PlayingSessionStateCallback callback)
    {
        _appState.CurrentPlayingAppID = callback.PlayingAppID;
        
        if (callback.PlayingAppID == 0)
        {
            _appState.CurrentGame = "";
        }
        else
        {
            if (_appState.TryGetAppName(callback.PlayingAppID, out string? cachedName) && !string.IsNullOrEmpty(cachedName))
            {
                _appState.CurrentGame = cachedName;
            }
            else
            {
                RequestAppInfo(callback.PlayingAppID);
                _appState.CurrentGame = "";
            }
        }
        
        if (_appState.FriendsListReceived)
        {
            Console.Clear();
            _displayManager.DisplayFriendsList(_steamFriends);
        }
    }

    public void OnFriendsList(SteamFriends.FriendsListCallback callback)
    {
        _appState.FriendsListReceived = true;

        for (int i = 0; i < _steamFriends.GetFriendCount(); i++)
        {
            SteamID steamIdFriend = _steamFriends.GetFriendByIndex(i);
            EFriendRelationship relationship = _steamFriends.GetFriendRelationship(steamIdFriend);

            if (relationship == EFriendRelationship.Friend)
            {
                _steamFriends.RequestFriendInfo(steamIdFriend,
                    EClientPersonaStateFlag.PlayerName |
                    EClientPersonaStateFlag.Presence |
                    EClientPersonaStateFlag.LastSeen |
                    EClientPersonaStateFlag.RichPresence |
                    EClientPersonaStateFlag.Status |
                    EClientPersonaStateFlag.GameExtraInfo |
                    EClientPersonaStateFlag.GameDataBlob |
                    EClientPersonaStateFlag.Watching |
                    EClientPersonaStateFlag.Broadcast |
                    EClientPersonaStateFlag.ClanData |
                    EClientPersonaStateFlag.UserClanRank |
                    EClientPersonaStateFlag.SourceID |
                    EClientPersonaStateFlag.QueryPort |
                    EClientPersonaStateFlag.Facebook);
            }
        }
    }

    public void OnPersonaState(SteamFriends.PersonaStateCallback callback)
    {
        if (!_appState.FriendsListReceived)
            return;

        // Check if this is our own persona state changing
        if (callback.FriendID == _steamClient.SteamID)
        {
            _appState.CurrentUserState = callback.State;
            Console.Clear();
            _displayManager.DisplayFriendsList(_steamFriends);
            return;
        }

        // Check if this is a friend
        EFriendRelationship relationship = _steamFriends.GetFriendRelationship(callback.FriendID);
        if (relationship == EFriendRelationship.Friend)
        {
            EPersonaState currentQueriedState = _steamFriends.GetFriendPersonaState(callback.FriendID);
            bool hadPreviousState = _appState.TryGetPersonaState(callback.FriendID, out EPersonaState lastState);

            bool hasStatusFlag = callback.StatusFlags.HasFlag(EClientPersonaStateFlag.Status);
            EPersonaState stateToTrack;

            if (hasStatusFlag)
            {
                stateToTrack = callback.State;
            }
            else if (hadPreviousState)
            {
                stateToTrack = lastState;
            }
            else
            {
                stateToTrack = currentQueriedState;
            }

            _appState.UpdatePersonaState(callback.FriendID, stateToTrack);

            if (callback.LastLogOff != DateTime.MinValue)
            {
                _appState.UpdateLastSeenTime(callback.FriendID, callback.LastLogOff);
            }

            Console.Clear();
            _displayManager.DisplayFriendsList(_steamFriends);
        }
    }

    public void OnPICSProductInfo(SteamApps.PICSProductInfoCallback callback)
    {
        foreach (var app in callback.Apps)
        {
            var appInfo = app.Value;
            if (appInfo.KeyValues != null && appInfo.KeyValues["common"] != null)
            {
                var name = appInfo.KeyValues["common"]["name"].AsString();
                if (!string.IsNullOrEmpty(name))
                {
                    _appState.UpdateAppName(app.Key, name);

                    if (app.Key == _appState.CurrentPlayingAppID && _appState.CurrentPlayingAppID != 0)
                    {
                        _appState.CurrentGame = name;
                    }

                    if (_appState.FriendsListReceived)
                    {
                        Console.Clear();
                        _displayManager.DisplayFriendsList(_steamFriends);
                    }
                }
            }
        }
    }

    public void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Console.WriteLine("Logged off of Steam: {0}", callback.Result);
    }

    private void RequestAppInfo(uint appId)
    {
        if (!_appState.ContainsApp(appId))
        {
            var request = new SteamApps.PICSRequest(appId);
            _steamApps.PICSGetProductInfo(new List<SteamApps.PICSRequest> { request }, new List<SteamApps.PICSRequest>());
        }
    }
}
