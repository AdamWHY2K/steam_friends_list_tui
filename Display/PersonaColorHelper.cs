using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

public static class PersonaColorHelper
{
    /// <summary>
    /// Gets the Spectre.Console color name for a persona state
    /// </summary>
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

    /// <summary>
    /// Gets the Spectre.Console color name for a persona state text (using PersonaStateHelper)
    /// </summary>
    public static string GetSpectreColorForPersonaState(string stateText)
    {
        // Convert text back to enum and use the main color function
        var state = stateText switch
        {
            "Online" => EPersonaState.Online,
            "Offline" => EPersonaState.Offline,
            "Busy" => EPersonaState.Busy,
            "Away" => EPersonaState.Away,
            "Snooze" => EPersonaState.Snooze,
            "Trading" => EPersonaState.LookingToTrade,
            "Looking" => EPersonaState.LookingToPlay,
            "Invisible" => EPersonaState.Invisible,
            _ => EPersonaState.Online
        };

        return GetSpectreColorForPersonaState(state);
    }
}
