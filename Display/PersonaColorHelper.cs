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
    /// Gets the Spectre.Console color name for a persona state text
    /// </summary>
    public static string GetSpectreColorForPersonaState(string stateText)
    {
        return stateText switch
        {
            "Online" => "green",
            "Offline" => "grey",
            "Busy" => "red",
            "Away" => "yellow",
            "Snooze" => "purple",
            "Trading" or "Looking" => "cyan",
            _ => "white"
        };
    }

    /// <summary>
    /// Gets friend colors for display, with special handling for gaming status
    /// </summary>
    public static string GetFriendDisplayColor(EPersonaState state, bool isPlayingGame)
    {
        // Friends playing games get bright green regardless of their status
        return isPlayingGame ? "green" : GetSpectreColorForPersonaState(state);
    }
}
