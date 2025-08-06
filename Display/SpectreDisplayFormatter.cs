using SteamKit2;
using Spectre.Console;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;

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
        var stateText = PersonaStateHelper.GetPersonaStateText(appState.CurrentUserState);
        var stateColor = GetSpectreColorForPersonaState(appState.CurrentUserState);
        var userInfo = $"[bold {stateColor}]{appState.CurrentPersonaName ?? "Loading..."}[/]";        
        userInfo += "\n  ";
        userInfo += !string.IsNullOrEmpty(appState.CurrentGame)
            ? $"[{stateColor}]{appState.CurrentGame}[/]"
            : $"[{stateColor}]{stateText}[/]";

        return userInfo;
    }
}
