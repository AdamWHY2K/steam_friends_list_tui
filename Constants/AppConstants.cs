namespace SteamFriendsCLI.Constants;

public static class AppConstants
{
    // Loading text constants
    public static class LoadingText
    {
        public const string Generic = "Loading...";
        public const string UserInfo = "Loading user info...";
        public const string GameName = "Loading game name...";
        public const string Connecting = "Connecting to Steam...";
        public const string NoFriendsFound = "No friends found";
    }

    // Timeout constants
    public static class Timeouts
    {
        public static readonly TimeSpan CallbackWait = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan GuiShutdown = TimeSpan.FromSeconds(2);
    }

    // UI Messages
    public static class Messages
    {
        public const string ConnectingToSteam = "Connecting to Steam...";
        public const string SteamRefreshChallenge = "Steam has refreshed the challenge url";
        public const string UseQrCode = "Use the Steam Mobile App to sign in via QR code:";
        public const string DisconnectedFromSteam = "Disconnected from Steam";
        public const string SuccessfullyLoggedOn = "Successfully logged on!";
    }
}
