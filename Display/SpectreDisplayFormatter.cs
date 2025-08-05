using SteamKit2;
using Spectre.Console;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

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

    public static string FormatFriendName(FriendInfo friend)
    {
        var color = GetSpectreColorForPersonaState(friend.State);      
        return $"[{color}]{friend.Name.EscapeMarkup()}[/]";
    }

    public static string FormatFriendStatus(FriendInfo friend)
    {
        var stateText = PersonaStateHelper.GetPersonaStateText(friend.State);
        var color = GetSpectreColorForPersonaState(friend.State);
        
        if (friend.State == EPersonaState.Offline && friend.LastSeen != DateTime.MinValue)
        {
            var lastSeenText = PersonaStateHelper.GetFormattedLastSeenText(friend.LastSeen);
            return $"[{color}]Last online {lastSeenText.EscapeMarkup()}[/]";
        }
        
        return $"[{color}]{stateText.EscapeMarkup()}[/]";
    }

    public static string FormatUserInfo(AppState appState)
    {
        var userInfo = $"{appState.CurrentPersonaName ?? "Loading..."}";
        
        var stateText = PersonaStateHelper.GetPersonaStateText(appState.CurrentUserState);
        var stateColor = GetSpectreColorForPersonaState(appState.CurrentUserState);
        
        if (!string.IsNullOrEmpty(appState.CurrentGame))
        {
            userInfo += $"\n  [{stateColor}]{appState.CurrentGame}[/]";
        }
        else
        {
            userInfo += $"\n  [{stateColor}]{stateText}[/]";
        }

        return userInfo;
    }
}
