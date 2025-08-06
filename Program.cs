using SteamFriendsTUI;

// Parse command line arguments
bool debugMode = args.Contains("--debug");

// Create and run the Steam Friends List TUI application
using var app = new SteamFriendsApp(debugMode);

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    app.Stop();
};

app.Run();
