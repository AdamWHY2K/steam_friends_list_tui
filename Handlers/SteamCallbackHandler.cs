using SteamKit2;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Display;
using SteamFriendsCLI.Services;
using SteamFriendsCLI.Constants;


namespace SteamFriendsCLI.Handlers;

public class SteamCallbackHandler
{
    private readonly SteamClient _steamClient;
    private readonly SteamUser _steamUser;
    private readonly SteamFriends _steamFriends;
    private readonly SteamApps _steamApps;
    private readonly AppState _appState;
    private readonly IFriendsDisplayManager _displayManager;
    private readonly ILogger _logger;
    
    // Event for authentication failure (e.g., expired tokens)
    public event Action? AuthenticationFailed;

    public SteamCallbackHandler(
        SteamClient steamClient,
        SteamUser steamUser,
        SteamFriends steamFriends,
        SteamApps steamApps,
        AppState appState,
        IFriendsDisplayManager displayManager,
        ILogger logger)
    {
        _steamClient = steamClient;
        _steamUser = steamUser;
        _steamFriends = steamFriends;
        _steamApps = steamApps;
        _appState = appState;
        _displayManager = displayManager;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine(AppConstants.Messages.DisconnectedFromSteam);
        _appState.IsRunning = false;
    }

    public void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);
            
            // Check if this is an authentication failure that might benefit from re-authentication
            if (callback.Result == EResult.AccessDenied || 
                callback.Result == EResult.InvalidLoginAuthCode || 
                callback.Result == EResult.AccountLoginDeniedNeedTwoFactor ||
                callback.Result == EResult.InvalidPassword)
            {
                Console.WriteLine("Authentication tokens may be expired. Clearing saved tokens...");
                TokenStorage.DeleteAuthTokens();
                AuthenticationFailed?.Invoke();
                return;
            }
            
            _appState.IsRunning = false;
            return;
        }
        
        Console.WriteLine(AppConstants.Messages.SuccessfullyLoggedOn);
        _appState.IsLoggedIn = true;
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
            _displayManager.DisplayFriendsList(_steamFriends);
        }
    }

    public void OnFriendsList(SteamFriends.FriendsListCallback callback)
    {
        _appState.FriendsListReceived = true;
        _logger.LogInfo("Friends list received from Steam...");

        // First, just get basic friend info with simpler flags
        SteamFriendsIterator.ForEachFriendOfType(_steamFriends, EFriendRelationship.Friend, steamIdFriend =>
        {
            _steamFriends.RequestFriendInfo(steamIdFriend,
                EClientPersonaStateFlag.PlayerName |
                EClientPersonaStateFlag.Presence |
                EClientPersonaStateFlag.Status |
                EClientPersonaStateFlag.LastSeen);
        });
        
        // Wait a moment and then trigger an initial display update
        Task.Delay(2000).ContinueWith(_ => 
        {
            Console.WriteLine("Initial delay complete, updating display...");
            _displayManager.DisplayFriendsList(_steamFriends);
        });
        
        Console.WriteLine("Requested friend info for all friends, waiting for persona state callbacks...");
    }

    public void OnPersonaState(SteamFriends.PersonaStateCallback callback)
    {
        if (!_appState.FriendsListReceived)
            return;

        Console.WriteLine($"Persona state callback received for: {callback.FriendID}");

        // Check if this is our own persona state changing
        if (callback.FriendID == _steamClient.SteamID)
        {
            _appState.CurrentUserState = callback.State;
            _displayManager.DisplayFriendsList(_steamFriends);
            return;
        }

        // Check if this is a friend
        EFriendRelationship relationship = _steamFriends.GetFriendRelationship(callback.FriendID);
        Console.WriteLine($"Friend {callback.FriendID} has relationship: {relationship}");
        
        if (relationship == EFriendRelationship.Friend)
        {
            var friendName = _steamFriends.GetFriendPersonaName(callback.FriendID);
            Console.WriteLine($"Processing persona state for friend: {friendName} ({callback.FriendID}) - State: {callback.State}");
            
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
                        _displayManager.DisplayFriendsList(_steamFriends);
                    }
                }
            }
        }
    }

    public void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        _appState.IsLoggedIn = false;
        _appState.IsRunning = false;
    }

    public void RequestAppInfo(uint appId)
    {
        if (!_appState.ContainsApp(appId))
        {
            // Add placeholder to prevent duplicate requests
            _appState.UpdateAppName(appId, AppConstants.LoadingText.Generic);
            
            var request = new SteamApps.PICSRequest(appId);
            _steamApps.PICSGetProductInfo(new List<SteamApps.PICSRequest> { request }, new List<SteamApps.PICSRequest>());
        }
    }
}
