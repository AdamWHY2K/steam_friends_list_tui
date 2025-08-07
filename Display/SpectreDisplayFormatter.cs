using Spectre.Console;
using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

public static class SpectreDisplayFormatter
{
    public static string GetSpectreColorForPersonaState(EPersonaState state)
    {
        return state switch
        {
            EPersonaState.Online => "green",
            EPersonaState.Busy => "red",
            EPersonaState.Away => "yellow",
            EPersonaState.Snooze => "purple",
            EPersonaState.LookingToTrade or EPersonaState.LookingToPlay => "cyan",
            EPersonaState.Offline => "grey",
            _ => "white"
        };
    }
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
        var color = GetSpectreColorForPersonaState(friend.State);
        var name = TruncateText(friend.Name, Console.WindowWidth - AppConstants.Display.NameWidthReduction);
        return $"[{color}]{name.EscapeMarkup()}[/]";
    }

    public static string FormatFriendStatus(FriendInfo friend)
    {
        var color = GetSpectreColorForPersonaState(friend.State);

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
        var stateText = PersonaStateHelper.GetPersonaStateText(appState.CurrentUserState);
        var stateColor = GetSpectreColorForPersonaState(appState.CurrentUserState);
        var userName = appState.CurrentPersonaName ?? AppConstants.LoadingText.Generic;
        userName = TruncateText(userName, Console.WindowWidth - AppConstants.Display.NameWidthReduction);
        var userInfo = $"[bold {stateColor}]{userName}[/]";

        var statusText = string.IsNullOrEmpty(appState.CurrentGame)
            ? stateText
            : $"{stateText} — {appState.CurrentGame}";
        statusText = TruncateText(statusText, Console.WindowWidth - AppConstants.Display.StatusWidthReduction);

        return userInfo + $"\n  [{stateColor}]{statusText.EscapeMarkup()}[/]";
    }
}
