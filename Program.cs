using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QRCoder;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using SteamKit2.WebUI.Internal;

// create our steamclient instance
var steamClient = new SteamClient();
// create the callback manager which will route callbacks to function calls
var manager = new CallbackManager( steamClient );

// get the steamuser handler, which is used for logging on after successfully connecting
var steamUser = steamClient.GetHandler<SteamUser>();
// get the steam friends handler, which is used for interacting with friends on the network after logging on
var steamFriends = steamClient.GetHandler<SteamFriends>();
// get the steam apps handler, which is used for getting app information
var steamApps = steamClient.GetHandler<SteamApps>();

// register a few callbacks we're interested in
// these are registered upon creation to a callback manager, which will then route the callbacks
// to the functions specified
manager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

manager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

var isRunning = true;
var friendsListReceived = false;
var lastPersonaStates = new Dictionary<SteamID, EPersonaState>();
var appNameCache = new Dictionary<uint, string>();
var lastSeenTimes = new Dictionary<SteamID, DateTime>();

// we use the following callbacks for friends related activities
manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
// manager.Subscribe<SteamUser.PlayingSessionStateCallback>( OnPlayingSession );
manager.Subscribe<SteamFriends.FriendsListCallback>( OnFriendsList );
manager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);
manager.Subscribe<SteamApps.PICSProductInfoCallback>(OnPICSProductInfo);


Console.WriteLine( "Connecting to Steam..." );

// initiate the connection
steamClient.Connect();

// create our callback handling loop
while ( isRunning )
{
    // in order for the callbacks to get routed, they need to be handled by the manager
    manager.RunWaitCallbacks( TimeSpan.FromSeconds( 1 ) );
}

async void OnConnected( SteamClient.ConnectedCallback callback )
{
    if (steamUser == null)
        return;
    // Start an authentication session by requesting a link
    var authSession = await steamClient.Authentication.BeginAuthSessionViaQRAsync( new AuthSessionDetails());

    // Steam will periodically refresh the challenge url, this callback allows you to draw a new qr code
    authSession.ChallengeURLChanged = () =>
    {
        Console.WriteLine();
        Console.WriteLine( "Steam has refreshed the challenge url" );

        DrawQRCode( authSession );
    };

    // Draw current qr right away
    DrawQRCode( authSession );

    // Starting polling Steam for authentication response
    // This response is later used to logon to Steam after connecting
    var pollResponse = await authSession.PollingWaitForResultAsync();

    Console.WriteLine( $"Logging in as '{pollResponse.AccountName}'..." );

    // Logon to Steam with the access token we have received
    steamUser.LogOn( new SteamUser.LogOnDetails
    {
        Username = pollResponse.AccountName,
        AccessToken = pollResponse.RefreshToken,
    } );
}

void OnDisconnected( SteamClient.DisconnectedCallback callback )
{
    Console.WriteLine( "Disconnected from Steam" );

    isRunning = false;
}

void OnLoggedOn( SteamUser.LoggedOnCallback callback )
{
    if (callback.Result != EResult.OK)
    {
        Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);
        isRunning = false;
        return;
    }
    Console.WriteLine("Successfully logged on!");
}

void OnAccountInfo( SteamUser.AccountInfoCallback callback )
{
    // before being able to interact with friends, you must wait for the account info callback
    // this callback is posted shortly after a successful logon

    // at this point, we can go online on friends, so lets do that
    if (steamFriends != null)
    {
        steamFriends.SetPersonaState(EPersonaState.Online);
    }
}

void OnFriendsList( SteamFriends.FriendsListCallback callback )
{
    // at this point, the client has received it's friends list
    friendsListReceived = true;
    
    if (steamFriends == null)
        return;
    
    // Request persona information
    for (int i = 0; i < steamFriends.GetFriendCount(); i++)
    {
        SteamID steamIdFriend = steamFriends.GetFriendByIndex(i);
        EFriendRelationship relationship = steamFriends.GetFriendRelationship(steamIdFriend);

        // Only request info for actual friends
        if (relationship == EFriendRelationship.Friend)
        {
            steamFriends.RequestFriendInfo(steamIdFriend,
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

void DisplayFriendsList()
{
    if (steamFriends == null)
        return;

    int totalCount = steamFriends.GetFriendCount();
    
    // Count actual friends (excluding blocked users, pending requests, etc.)
    int actualFriendCount = 0;
    int blockedCount = 0;
    int pendingCount = 0;
    int ignoredCount = 0;
    
    for ( int i = 0 ; i < totalCount ; i++ )
    {
        SteamID steamIdFriend = steamFriends.GetFriendByIndex( i );
        EFriendRelationship relationship = steamFriends.GetFriendRelationship( steamIdFriend );
        
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

    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                                 STEAM FRIENDS LIST                                ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║ {actualFriendCount} friends, {pendingCount} pending, {blockedCount} blocked, {ignoredCount} ignored{"",-34} ║");
    
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

    if (actualFriendCount == 0)
    {
        Console.WriteLine("║                              No friends found                                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
        return;
    }

    Console.WriteLine("║                                                                                ║");

    // Create a list to hold friend data for sorting
    var friendsList = new List<(SteamID steamId, string name, EPersonaState state, string statusText, string statusColor, string gameText, DateTime lastSeen)>();
    
    for ( int x = 0 ; x < totalCount ; x++ )
    {
        // steamids identify objects that exist on the steam network, such as friends, as an example
        SteamID steamIdFriend = steamFriends.GetFriendByIndex( x );
        
        // Get the relationship type
        EFriendRelationship relationship = steamFriends.GetFriendRelationship( steamIdFriend );
        
        // Only display actual friends - exclude blocked users, pending requests, ignored users, etc.
        if (relationship == EFriendRelationship.Friend)
        {
            string? friendName = steamFriends.GetFriendPersonaName( steamIdFriend );
            string statusText;
            string statusColor;
            string gameText = "";
            
            // Only show actual status if we have persona data, otherwise show "Loading..."
            if (!string.IsNullOrEmpty(friendName))
            {
                // Use our tracked state instead of directly querying Steam (which may be unreliable)
                EPersonaState friendState;
                if (lastPersonaStates.TryGetValue(steamIdFriend, out EPersonaState trackedState))
                {
                    friendState = trackedState;
                }
                else
                {
                    // Fallback to queried state if we haven't tracked this friend yet
                    friendState = steamFriends.GetFriendPersonaState( steamIdFriend );
                }
                
                string baseStatus = GetPersonaStateText(friendState);
                statusColor = GetPersonaStateColor(friendState);
                
                // For offline friends, add last seen information
                if (friendState == EPersonaState.Offline)
                {
                    if (lastSeenTimes.TryGetValue(steamIdFriend, out DateTime lastSeenValue) && lastSeenValue != DateTime.MinValue)
                    {
                        var timeDiff = DateTime.Now - lastSeenValue;
                        string lastSeenText = GetLastSeenText(timeDiff);
                        statusText = $"{baseStatus} - Last online {lastSeenText}";
                    }
                    else
                    {
                        statusText = baseStatus;
                    }
                }
                else
                {
                    // Get game information and combine with status
                    string? gameName = steamFriends.GetFriendGamePlayedName( steamIdFriend );
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        statusText = $"{baseStatus} - {gameName}";
                        gameText = gameName;
                    }
                    else
                    {
                        statusText = baseStatus;
                        
                        // Check if they're playing a game but we can't get the name
                        var gameId = steamFriends.GetFriendGamePlayed(steamIdFriend);
                        if (gameId != null && gameId.AppID != 0)
                        {
                            // Check our cache first
                            if (appNameCache.TryGetValue(gameId.AppID, out string? cachedName))
                            {
                                statusText = $"{baseStatus} - {cachedName}";
                                gameText = cachedName;
                            }
                            else
                            {
                                // Request app info if we don't have it
                                RequestAppInfo(gameId.AppID);
                                statusText = $"{baseStatus} - Game ID: {gameId.AppID}";
                                gameText = $"Game ID: {gameId.AppID}";
                            }
                        }
                    }
                }
                
                // Get last seen time for sorting
                DateTime lastSeenTime = lastSeenTimes.TryGetValue(steamIdFriend, out DateTime lastSeen) ? lastSeen : DateTime.MinValue;

                friendsList.Add((steamIdFriend, friendName, friendState, statusText, statusColor, gameText, lastSeenTime));
            }
            else
            {
                friendName = "Loading...";
                statusText = "Loading...";
                statusColor = "\u001b[90m"; // Dark gray
                friendsList.Add((steamIdFriend, friendName, EPersonaState.Offline, statusText, statusColor, "", DateTime.MinValue));
            }
        }
    }
    
    var sortedFriends = friendsList.OrderBy(f => string.IsNullOrEmpty(f.gameText) ? 1 : 0)  // Playing games first (0), then not playing (1)
                                   .ThenBy(f => f.gameText)  // Then by game name alphabetically
                                   .ThenBy(f => GetStatusSortOrder(f.state))  // Status priority
                                   .ThenByDescending(f => f.lastSeen)  // Most recent last seen first
                                   .ToList();
    
    foreach (var friend in sortedFriends)
    {
        string friendName = friend.name;
        string statusText = friend.statusText;
        string statusColor = friend.statusColor;
        
        // Truncate name if too long
        if (friendName.Length > 20)
            friendName = friendName.Substring(0, 17) + "...";
            
        // Truncate status text if too long
        if (statusText.Length > 54)
            statusText = statusText.Substring(0, 51) + "...";
        
        // Create the line with proper spacing
        string line = $"║ {friendName,-20} {statusColor}{statusText}\u001b[0m";
        // Pad to full width
        while (line.Length < 81) line += " ";
        line += " ║";
        
        Console.WriteLine(line);
    }
    
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}

void OnPersonaState( SteamFriends.PersonaStateCallback callback )
{
    // this callback is received when the persona state (friend information) of a friend changes
    if (friendsListReceived && steamFriends != null)
    {
        // Check if this is a friend (not blocked, pending, etc.)
        EFriendRelationship relationship = steamFriends.GetFriendRelationship(callback.FriendID);
        
        if (relationship == EFriendRelationship.Friend)
        {
            // Get the current state from SteamFriends (what we query vs what callback reports)
            EPersonaState currentQueriedState = steamFriends.GetFriendPersonaState(callback.FriendID);
            
            // Get previous state for comparison
            bool hadPreviousState = lastPersonaStates.TryGetValue(callback.FriendID, out EPersonaState lastState);
            string previousStateText = hadPreviousState ? lastState.ToString() : "None";
            
            // Determine which state to trust based on callback flags
            bool hasStatusFlag = callback.StatusFlags.HasFlag(EClientPersonaStateFlag.Status);
            EPersonaState stateToTrack;
            
            if (hasStatusFlag)
            {
                // If callback has Status flag, we trust the callback state
                stateToTrack = callback.State;
            }
            else if (hadPreviousState)
            {
                // If no Status flag but we have previous state, keep the previous state
                stateToTrack = lastState;
            }
            else
            {
                // Fallback to queried state for first-time data
                stateToTrack = currentQueriedState;
            }
            
            // Check if this is actually a status change
            bool isActualChange = hadPreviousState && lastState != stateToTrack;
            
            // Update our tracking with the determined state
            lastPersonaStates[callback.FriendID] = stateToTrack;
            
            // Store last logoff time if available (for offline friends)
            if (callback.LastLogOff != DateTime.MinValue)
            {
                lastSeenTimes[callback.FriendID] = callback.LastLogOff;
            }
            
            // Always update the friends list display with latest information
            Console.Clear();
            DisplayFriendsList();
        }
    }
}

void OnPICSProductInfo(SteamApps.PICSProductInfoCallback callback)
{
    foreach (var app in callback.Apps)
    {
        var appInfo = app.Value;
        if (appInfo.KeyValues != null && appInfo.KeyValues["common"] != null)
        {
            var name = appInfo.KeyValues["common"]["name"].AsString();
            if (!string.IsNullOrEmpty(name))
            {
                appNameCache[app.Key] = name;
                
                // Refresh display if we got new app info
                if (friendsListReceived)
                {
                    Console.Clear();
                    DisplayFriendsList();
                }
            }
        }
    }
}

void RequestAppInfo(uint appId)
{
    if (!appNameCache.ContainsKey(appId) && steamApps != null)
    {
        var request = new SteamApps.PICSRequest(appId);
        steamApps.PICSGetProductInfo(new List<SteamApps.PICSRequest> { request }, new List<SteamApps.PICSRequest>());
    }
}

void OnLoggedOff( SteamUser.LoggedOffCallback callback )
{
    Console.WriteLine( "Logged off of Steam: {0}", callback.Result );
}

void DrawQRCode( QrAuthSession authSession )
{
    Console.WriteLine( $"Challenge URL: {authSession.ChallengeURL}" );
    Console.WriteLine();

    // Encode the link as a QR code
    using var qrGenerator = new QRCodeGenerator();
    var qrCodeData = qrGenerator.CreateQrCode( authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L );
    using var qrCode = new AsciiQRCode( qrCodeData );
    var qrCodeAsAsciiArt = qrCode.GetGraphic( 1, drawQuietZones: false );

    Console.WriteLine( "Use the Steam Mobile App to sign in via QR code:" );
    Console.WriteLine( qrCodeAsAsciiArt );
}

string GetPersonaStateText(EPersonaState state)
{
    return state switch
    {
        EPersonaState.Offline => "Offline",
        EPersonaState.Online => "Online",
        EPersonaState.Busy => "Busy",
        EPersonaState.Away => "Away",
        EPersonaState.Snooze => "Snooze",
        EPersonaState.LookingToTrade => "Trading",
        EPersonaState.LookingToPlay => "Looking",
        EPersonaState.Invisible => "Invisible",
        _ => "Unknown"
    };
}

string GetPersonaStateColor(EPersonaState state)
{
    return state switch
    {
        EPersonaState.Online => "\u001b[32m",      // Green
        EPersonaState.Busy => "\u001b[31m",        // Red
        EPersonaState.Away => "\u001b[33m",        // Yellow
        EPersonaState.Snooze => "\u001b[35m",      // Magenta
        EPersonaState.LookingToTrade => "\u001b[36m", // Cyan
        EPersonaState.LookingToPlay => "\u001b[34m",  // Blue
        EPersonaState.Invisible => "\u001b[90m",    // Dark Gray
        _ => "\u001b[37m"                          // White/Default
    };
}

string GetLastSeenText(TimeSpan timeDiff)
{
    if (timeDiff.TotalMinutes < 1)
        return "moments ago";
    else if (timeDiff.TotalMinutes < 60)
        return $"{(int)timeDiff.TotalMinutes} minute{((int)timeDiff.TotalMinutes == 1 ? "" : "s")} ago";
    else if (timeDiff.TotalHours < 24)
        return $"{(int)timeDiff.TotalHours} hour{((int)timeDiff.TotalHours == 1 ? "" : "s")} ago";
    else if (timeDiff.TotalDays < 7)
        return $"{(int)timeDiff.TotalDays} day{((int)timeDiff.TotalDays == 1 ? "" : "s")} ago";
    else if (timeDiff.TotalDays < 30)
        return $"{(int)(timeDiff.TotalDays / 7)} week{((int)(timeDiff.TotalDays / 7) == 1 ? "" : "s")} ago";
    else if (timeDiff.TotalDays < 365)
        return $"{(int)(timeDiff.TotalDays / 30)} month{((int)(timeDiff.TotalDays / 30) == 1 ? "" : "s")} ago";
    else
        return $"{(int)(timeDiff.TotalDays / 365)} year{((int)(timeDiff.TotalDays / 365) == 1 ? "" : "s")} ago";
}

int GetStatusSortOrder(EPersonaState state)
{
    return state switch
    {
        EPersonaState.Online => 1,
        EPersonaState.LookingToPlay => 2,
        EPersonaState.LookingToTrade => 3,
        EPersonaState.Away => 4,
        EPersonaState.Busy => 5,
        EPersonaState.Snooze => 6,
        EPersonaState.Invisible => 7,
        EPersonaState.Offline => 8,
        _ => 9
    };
}
