using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Display;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;


namespace SteamFriendsTUI.Handlers;

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
        _logger.LogInfo(AppConstants.Messages.DisconnectedFromSteam);
        _appState.SetConnected(false);
    }

    public void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            _logger.LogError($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");

            // Check if this is an authentication failure that might benefit from re-authentication
            if (callback.Result == EResult.AccessDenied ||
                callback.Result == EResult.InvalidLoginAuthCode ||
                callback.Result == EResult.AccountLoginDeniedNeedTwoFactor ||
                callback.Result == EResult.InvalidPassword)
            {
                _logger.LogWarning("Authentication tokens may be expired. Clearing saved tokens...");
                TokenStorage.DeleteAuthTokens();
                AuthenticationFailed?.Invoke();
                return;
            }

            _appState.IsRunning = false;
            return;
        }

        _logger.LogInfo(AppConstants.Messages.SuccessfullyLoggedOn);
        _appState.IsLoggedIn = true;
        
        if (_appState.LastDisconnectedTime.HasValue)
        {
            _displayManager.UpdateConnectionStatus("Reconnected to Steam - Loading friends list...");
            _displayManager.DisplayFriendsList(_steamFriends);
        }
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
            _logger.LogDebug("Display update triggered: Connection status changed");
            _displayManager.DisplayFriendsList(_steamFriends);
        }
    }

    public void OnFriendsList(SteamFriends.FriendsListCallback callback)
    {
        _appState.FriendsListReceived = true;
        _logger.LogInfo("Friends list received from Steam...");

        // Request comprehensive friend info including game status
        SteamFriendsIterator.ForEachFriendOfType(_steamFriends, EFriendRelationship.Friend, steamIdFriend =>
        {
            _steamFriends.RequestFriendInfo(steamIdFriend,
                EClientPersonaStateFlag.PlayerName |
                EClientPersonaStateFlag.Presence |
                EClientPersonaStateFlag.Status |
                EClientPersonaStateFlag.LastSeen |
                EClientPersonaStateFlag.GameExtraInfo |
                EClientPersonaStateFlag.GameDataBlob |
                EClientPersonaStateFlag.RichPresence);
        });

        // Wait a moment and then trigger an initial display update
        Task.Delay(2000).ContinueWith(_ =>
        {
            _logger.LogDebug("Display update triggered: Initial friends list setup after delay");
            _displayManager.DisplayFriendsList(_steamFriends);
        });

        _logger.LogDebug("Requested friend info for all friends, waiting for persona state callbacks...");
    }

    public void OnPersonaState(SteamFriends.PersonaStateCallback callback)
    {
        if (!_appState.FriendsListReceived)
            return;

        var statusFlags = string.Join(", ", callback.StatusFlags.ToString().Split(',').Select(f => f.Trim()));
        _logger.LogDebug($"Persona state callback received for: {callback.FriendID} - State: {callback.State}, Flags: [{statusFlags}], LastLogOff: {callback.LastLogOff}");

        // Check if this is our own persona state changing
        if (callback.FriendID == _steamClient.SteamID)
        {
            // Only update display if the state actually changed
            if (_appState.CurrentUserState != callback.State)
            {
                _appState.CurrentUserState = callback.State;
                _logger.LogDebug($"Display update triggered: Own persona state changed to {callback.State}");
                _displayManager.DisplayFriendsList(_steamFriends);
            }
            else
            {
                _logger.LogDebug($"Own persona state callback received but state unchanged: {callback.State}");
            }
            return;
        }

        // Check if this is a friend
        EFriendRelationship relationship = _steamFriends.GetFriendRelationship(callback.FriendID);
        _logger.LogDebug($"Friend {callback.FriendID} has relationship: {relationship}");

        if (relationship == EFriendRelationship.Friend)
        {
            var friendName = _steamFriends.GetFriendPersonaName(callback.FriendID);
            _logger.LogDebug($"Processing persona state for friend: {friendName} ({callback.FriendID}) - State: {callback.State}");

            EPersonaState currentQueriedState = _steamFriends.GetFriendPersonaState(callback.FriendID);
            bool hadPreviousState = _appState.TryGetPersonaState(callback.FriendID, out EPersonaState lastState);

            bool hasStatusFlag = callback.StatusFlags.HasFlag(EClientPersonaStateFlag.Status);
            bool hasGameExtraInfoFlag = callback.StatusFlags.HasFlag(EClientPersonaStateFlag.GameExtraInfo);
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

            // Check if game status has changed - refresh app info if needed
            if (hasGameExtraInfoFlag)
            {
                var gameId = _steamFriends.GetFriendGamePlayed(callback.FriendID);
                if (gameId != null && gameId.AppID != 0)
                {
                    if (!_appState.TryGetAppName(gameId.AppID, out string? cachedName) || string.IsNullOrEmpty(cachedName))
                    {
                        RequestAppInfo(gameId.AppID);
                    }
                }
            }

            // Determine if anything actually changed that would warrant a display update
            var updateReasons = new List<string>();
            bool stateChanged = !hadPreviousState || lastState != stateToTrack;

            if (hasStatusFlag && stateChanged) updateReasons.Add($"status changed to {callback.State}");
            if (hasGameExtraInfoFlag) updateReasons.Add("game info updated");
            if (callback.LastLogOff != DateTime.MinValue) updateReasons.Add("last seen time updated");

            // Only trigger display update if there are meaningful changes
            if (updateReasons.Count > 0)
            {
                var reasonText = string.Join(", ", updateReasons);
                _logger.LogDebug($"Display update triggered: Friend {friendName} ({callback.FriendID}) - {reasonText}");
                _displayManager.DisplayFriendsList(_steamFriends);
            }
            else
            {
                _logger.LogDebug($"Persona state callback received for {friendName} but no meaningful changes detected");
            }
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
                        _logger.LogDebug($"Display update triggered: App info received for {name} (AppID: {app.Key})");
                        _displayManager.DisplayFriendsList(_steamFriends);
                    }
                }
            }
        }
    }

    public void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        _logger.LogInfo($"Logged off of Steam: {callback.Result}");
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
