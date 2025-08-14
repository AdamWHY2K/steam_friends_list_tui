using Spectre.Console;
using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

public static class SpectreDisplayFormatter
{
    public static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }
        return text.Substring(0, maxLength - 1) + "…";
    }

    public static string FormatFriendName(FriendInfo friend)
    {
        var color = PersonaColorHelper.GetSpectreColorForPersonaState(friend.State);
        var name = TruncateText(friend.Name, Console.WindowWidth - AppConstants.Display.NameWidthReduction);
        return $"[{color}]{name.EscapeMarkup()}[/]";
    }

    public static string FormatFriendStatus(FriendInfo friend)
    {
        var color = PersonaColorHelper.GetSpectreColorForPersonaState(friend.State);

        // For offline friends, dynamically calculate the "last seen" time to ensure it updates
        string statusText;
        if (friend.State == EPersonaState.Offline && friend.LastSeen != DateTime.MinValue)
        {
            statusText = PersonaStateHelper.GetCompleteStatusText(friend.State, friend.LastSeen);
        }
        else
        {
            // Use the cached StatusText field for online friends (contains game information)
            statusText = friend.StatusText;
        }

        statusText = TruncateText(statusText, Console.WindowWidth - AppConstants.Display.StatusWidthReduction);
        return $"[{color}]{statusText.EscapeMarkup()}[/]";
    }

    public static string FormatUserInfo(AppState appState)
    {
        return !appState.IsConnected
            ? FormatDisconnectedUserInfo(appState)
            : FormatConnectedUserInfo(appState);
    }

    private static string FormatDisconnectedUserInfo(AppState appState)
    {
        var disconnectionText = GetDisconnectionStatusText(appState);
        var userName = GetFormattedUserName(appState.CurrentPersonaName, "Steam User");

        var userInfoMarkup = $"[bold red]{userName}[/]";
        var statusMarkup = FormatStatusText(disconnectionText, "red");

        return $"{userInfoMarkup}{Environment.NewLine}  {statusMarkup}";
    }

    private static string FormatConnectedUserInfo(AppState appState)
    {
        var stateText = PersonaStateHelper.GetPersonaStateText(appState.CurrentUserState);
        var stateColor = PersonaColorHelper.GetSpectreColorForPersonaState(appState.CurrentUserState);
        var userName = GetFormattedUserName(appState.CurrentPersonaName, AppConstants.LoadingText.Generic);

        var userInfoMarkup = $"[bold {stateColor}]{userName}[/]";
        var statusText = string.IsNullOrEmpty(appState.CurrentGame)
            ? stateText
            : $"{stateText} — {appState.CurrentGame}";
        var statusMarkup = FormatStatusText(statusText, stateColor, escapeMarkup: true);

        return $"{userInfoMarkup}{Environment.NewLine}  {statusMarkup}";
    }

    private static string GetDisconnectionStatusText(AppState appState)
    {
        var timeSinceDisconnection = appState.GetTimeSinceDisconnection();
        if (!timeSinceDisconnection.HasValue)
            return "Steam Disconnected";

        var timeText = PersonaStateHelper.GetLastSeenText(timeSinceDisconnection.Value);
        return $"Disconnected {timeText}";
    }

    private static string GetFormattedUserName(string? currentName, string fallbackName)
    {
        var userName = !string.IsNullOrEmpty(currentName) ? currentName : fallbackName;
        return TruncateText(userName, Console.WindowWidth - AppConstants.Display.NameWidthReduction);
    }

    private static string FormatStatusText(string statusText, string color, bool escapeMarkup = false)
    {
        var truncatedText = TruncateText(statusText, Console.WindowWidth - AppConstants.Display.StatusWidthReduction);
        var finalText = escapeMarkup ? truncatedText.EscapeMarkup() : truncatedText;
        return $"[{color}]{finalText}[/]";
    }
}
