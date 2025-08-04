using SteamKit2;

namespace SteamFriendsCLI.Display;

public interface IFriendsDisplayManager : IDisposable
{
    void DisplayFriendsList(SteamFriends? steamFriends);
    void UpdateConnectionStatus(string status);
    void Initialize();
    void Run();
    void Stop();
}
