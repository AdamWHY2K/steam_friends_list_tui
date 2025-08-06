using SteamKit2;

namespace SteamFriendsTUI.Display;

public interface IFriendsDisplayManager : IDisposable
{
    void DisplayFriendsList(SteamFriends? steamFriends);
    void UpdateConnectionStatus(string status);
    void RefreshDisplay(bool resetScroll = false);
    void Initialize();
    void Run();
    void Stop();
    
    // Event for requesting app info when game names are not cached
    event Action<uint>? AppInfoRequested;
    
    // Event for requesting application exit
    event Action? ExitRequested;
}
