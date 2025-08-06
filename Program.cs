using SteamFriendsTUI;

// Create and run the Steam Friends List TUI application
using var app = new SteamFriendsApp();

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    app.Stop();
};

app.Run();
