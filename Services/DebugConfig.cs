namespace SteamFriendsTUI.Services;

/// <summary>
/// Global configuration for debug mode settings
/// </summary>
public static class DebugConfig
{
    /// <summary>
    /// Indicates whether debug mode is enabled
    /// When true: only console messages are shown
    /// When false: only the friends list is shown
    /// </summary>
    public static bool IsDebugMode { get; set; } = false;
}
