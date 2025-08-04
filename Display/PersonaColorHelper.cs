using Terminal.Gui;
using SteamKit2;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

public static class PersonaColorHelper
{
    /// <summary>
    /// Gets the foreground color for a persona state
    /// </summary>
    public static Color GetPersonaStateColor(EPersonaState state)
    {
        return state switch
        {
            EPersonaState.Online => Color.BrightGreen,
            EPersonaState.Busy => Color.BrightRed,
            EPersonaState.Away => Color.BrightYellow,
            EPersonaState.Snooze => Color.BrightMagenta,
            EPersonaState.LookingToTrade or EPersonaState.LookingToPlay => Color.BrightCyan,
            EPersonaState.Offline => Color.Gray,
            _ => Color.White
        };
    }

    /// <summary>
    /// Gets the foreground color for a persona state text
    /// </summary>
    public static Color GetPersonaStateColor(string stateText)
    {
        return stateText switch
        {
            "Online" => Color.BrightGreen,
            "Offline" => Color.Gray,
            "Busy" => Color.BrightRed,
            "Away" => Color.BrightYellow,
            "Snooze" => Color.BrightMagenta,
            "Trading" or "Looking" => Color.BrightCyan,
            _ => Color.White
        };
    }

    /// <summary>
    /// Gets friend colors for list display, with special handling for gaming status
    /// </summary>
    public static (Color foreground, Color background) GetFriendColors(EPersonaState state, bool isPlayingGame, bool isSelected)
    {
        Color backgroundColor = isSelected ? Color.DarkGray : Color.Black;
        
        // Friends playing games get bright green regardless of their status
        Color foregroundColor = isPlayingGame ? Color.BrightGreen : GetPersonaStateColor(state);

        return (foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Creates a color scheme for UI components based on persona state
    /// </summary>
    public static ColorScheme CreatePersonaColorScheme(string stateText)
    {
        var foregroundColor = GetPersonaStateColor(stateText);
        var colorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(foregroundColor, Color.Black),
            Focus = new Terminal.Gui.Attribute(foregroundColor, Color.DarkGray)
        };
        return colorScheme;
    }

    /// <summary>
    /// Creates a color scheme for UI components based on persona state
    /// </summary>
    public static ColorScheme CreatePersonaColorScheme(EPersonaState state)
    {
        var stateText = PersonaStateHelper.GetPersonaStateText(state);
        return CreatePersonaColorScheme(stateText);
    }
}
