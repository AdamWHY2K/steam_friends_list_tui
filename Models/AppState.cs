using SteamKit2;

namespace SteamFriendsTUI.Models;

public class AppState
{
    public bool IsRunning { get; set; } = true;
    public bool IsLoggedIn { get; set; } = false;
    public bool IsConnected { get; set; } = false;
    public DateTime? LastDisconnectedTime { get; set; } = null;
    public string CurrentPersonaName { get; set; } = string.Empty;
    public string CurrentGame { get; set; } = string.Empty;
    public uint CurrentPlayingAppID { get; set; } = 0u;
    public EPersonaState CurrentUserState { get; set; } = EPersonaState.Online;
    public bool FriendsListReceived { get; set; } = false;

    // Dictionaries for tracking data
    public Dictionary<SteamID, EPersonaState> LastPersonaStates { get; } = new();
    public Dictionary<uint, string> AppNameCache { get; } = new();
    public Dictionary<SteamID, DateTime> LastSeenTimes { get; } = new();

    // Thread safety
    private readonly object _stateLock = new object();

    public void UpdatePersonaState(SteamID steamId, EPersonaState state)
    {
        lock (_stateLock)
        {
            LastPersonaStates[steamId] = state;
        }
    }

    public void UpdateLastSeenTime(SteamID steamId, DateTime lastSeen)
    {
        lock (_stateLock)
        {
            LastSeenTimes[steamId] = lastSeen;
        }
    }

    public void UpdateAppName(uint appId, string name)
    {
        lock (_stateLock)
        {
            AppNameCache[appId] = name;
        }
    }

    public bool TryGetPersonaState(SteamID steamId, out EPersonaState state)
    {
        lock (_stateLock)
        {
            return LastPersonaStates.TryGetValue(steamId, out state);
        }
    }

    public bool TryGetLastSeenTime(SteamID steamId, out DateTime lastSeen)
    {
        lock (_stateLock)
        {
            return LastSeenTimes.TryGetValue(steamId, out lastSeen);
        }
    }

    public bool TryGetAppName(uint appId, out string? name)
    {
        lock (_stateLock)
        {
            return AppNameCache.TryGetValue(appId, out name);
        }
    }

    public bool ContainsApp(uint appId)
    {
        lock (_stateLock)
        {
            return AppNameCache.ContainsKey(appId);
        }
    }

    public void SetConnected(bool connected)
    {
        lock (_stateLock)
        {
            IsConnected = connected;
            
            if (connected)
            {
                LastDisconnectedTime = null;
            }
            else
            {
                IsLoggedIn = false;
                if (!LastDisconnectedTime.HasValue)
                {
                    LastDisconnectedTime = DateTime.Now;
                }
            }
        }
    }

    public TimeSpan? GetTimeSinceDisconnection()
    {
        lock (_stateLock)
        {
            return LastDisconnectedTime.HasValue ? DateTime.Now - LastDisconnectedTime.Value : null;
        }
    }
}
