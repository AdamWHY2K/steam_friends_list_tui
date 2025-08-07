namespace SteamFriendsTUI.Constants;

public static class AppConstants
{
    // Loading text constants
    public static class LoadingText
    {
        public const string Generic = "Loading…";
        public const string Connecting = "Connecting to Steam…";
        public const string NoFriendsFound = "No friends found";
        public const string GameName = "Loading game name…";
    }

    // Timeout constants
    public static class Timeouts
    {
        public static readonly TimeSpan CallbackWait = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan GuiShutdown = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan InputCheckInterval = TimeSpan.FromMilliseconds(100);
    }

    // Display constants
    public static class Display
    {
        // Text truncation width calculations
        public const int NameWidthReduction = 6; // 2 for borders, 3 for padding, 1 for ellipsis
        public const int StatusWidthReduction = 8; // 2 for borders, 3 for padding, 2 for indentation, 1 for ellipsis
    }

    public static class Token
    {
        public const int TokenExpirationDays = 30; // Tokens expire after 30 days
    }

    // UI Messages
    public static class Messages
    {
        public const string SteamRefreshChallenge = "Steam has refreshed the challenge url";
        public const string UseQrCode = "Use the Steam Mobile App to sign in via QR code:";
        public const string DisconnectedFromSteam = "Disconnected from Steam";
        public const string SuccessfullyLoggedOn = "Successfully logged on!";
    }
}
