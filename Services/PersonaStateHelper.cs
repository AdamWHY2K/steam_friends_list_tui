using SteamKit2;

namespace SteamFriendsCLI.Services;

public static class PersonaStateHelper
{
    public static string GetPersonaStateText(EPersonaState state)
    {
        return state switch
        {
            EPersonaState.Offline => "Offline",
            EPersonaState.Online => "Online",
            EPersonaState.Busy => "Busy",
            EPersonaState.Away => "Away",
            EPersonaState.Snooze => "Snooze",
            EPersonaState.LookingToTrade => "Trading",
            EPersonaState.LookingToPlay => "Looking",
            EPersonaState.Invisible => "Invisible",
            _ => "Unknown"
        };
    }

    public static int GetStatusSortOrder(EPersonaState state)
    {
        return state switch
        {
            EPersonaState.Online => 1,
            EPersonaState.LookingToPlay => 2,
            EPersonaState.LookingToTrade => 3,
            EPersonaState.Away => 4,
            EPersonaState.Busy => 5,
            EPersonaState.Snooze => 6,
            EPersonaState.Invisible => 7,
            EPersonaState.Offline => 8,
            _ => 9
        };
    }

    public static string GetLastSeenText(TimeSpan timeDiff)
    {
        if (timeDiff.TotalMinutes < 1)
            return "moments ago";
        else if (timeDiff.TotalMinutes < 60)
            return $"{(int)timeDiff.TotalMinutes} minute{((int)timeDiff.TotalMinutes == 1 ? "" : "s")} ago";
        else if (timeDiff.TotalHours < 24)
            return $"{(int)timeDiff.TotalHours} hour{((int)timeDiff.TotalHours == 1 ? "" : "s")} ago";
        else if (timeDiff.TotalDays < 7)
            return $"{(int)timeDiff.TotalDays} day{((int)timeDiff.TotalDays == 1 ? "" : "s")} ago";
        else if (timeDiff.TotalDays < 30)
            return $"{(int)(timeDiff.TotalDays / 7)} week{((int)(timeDiff.TotalDays / 7) == 1 ? "" : "s")} ago";
        else if (timeDiff.TotalDays < 365)
            return $"{(int)(timeDiff.TotalDays / 30)} month{((int)(timeDiff.TotalDays / 30) == 1 ? "" : "s")} ago";
        else
            return $"{(int)(timeDiff.TotalDays / 365)} year{((int)(timeDiff.TotalDays / 365) == 1 ? "" : "s")} ago";
    }

    /// <summary>
    /// Gets formatted last seen text for a given DateTime, handling the time difference calculation
    /// </summary>
    public static string GetFormattedLastSeenText(DateTime lastSeen)
    {
        if (lastSeen == DateTime.MinValue)
            return "Unknown";
            
        var timeDiff = DateTime.Now - lastSeen;
        return GetLastSeenText(timeDiff);
    }

    /// <summary>
    /// Creates a complete status text with last seen information for offline users
    /// </summary>
    public static string GetCompleteStatusText(EPersonaState state, DateTime lastSeen)
    {
        var baseStatus = GetPersonaStateText(state);
        
        if (state == EPersonaState.Offline && lastSeen != DateTime.MinValue)
        {
            var lastSeenText = GetFormattedLastSeenText(lastSeen);
            return $"Last online {lastSeenText}";
        }
        
        return baseStatus;
    }
}
