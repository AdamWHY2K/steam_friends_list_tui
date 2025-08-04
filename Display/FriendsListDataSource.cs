using System.Text;
using System.Linq;
using Terminal.Gui;
using SteamKit2;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

public class FriendsListDataSource : IListDataSource
{
    private List<FriendInfo> _friends = new();

    public int Count => _friends.Count;
    public int Length => _friends.Count;

    public bool IsMarked(int item) => false;

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
    {
        if (item >= _friends.Count) return;

        var friend = _friends[item];
        var text = FormatFriendForDisplay(friend);
        
        // Get color based on friend status
        var (foregroundColor, backgroundColor) = PersonaColorHelper.GetFriendColors(
            friend.State, 
            !string.IsNullOrEmpty(friend.GameText), 
            selected);
        
        // Set the colors
        driver.SetAttribute(new Terminal.Gui.Attribute(foregroundColor, backgroundColor));
        
        // Render the text
        var displayText = text.Length > width ? text.Substring(start, Math.Min(width, text.Length - start)) : text.Substring(start);
        driver.AddStr(displayText);
        
        // Fill remaining space with background color
        for (int i = displayText.Length; i < width; i++)
        {
            driver.AddRune(' ');
        }
    }

    public void SetMark(int item, bool value)
    {
        // Not implementing marking functionality
    }

    public System.Collections.IList ToList() => _friends.Cast<object>().ToList();

    public void UpdateFriends(List<FriendInfo> friends)
    {
        _friends = friends;
    }

    private string FormatFriendForDisplay(FriendInfo friend)
    {
        var nameAndStatus = $"{friend.Name}";
        
        if (!string.IsNullOrEmpty(friend.GameText))
        {
            nameAndStatus += $" - {friend.GameText}";
        }
        else
        {
            var stateText = PersonaStateHelper.GetPersonaStateText(friend.State);
            if (friend.State == EPersonaState.Offline && friend.LastSeen != DateTime.MinValue)
            {
                var lastSeenText = PersonaStateHelper.GetFormattedLastSeenText(friend.LastSeen);
                nameAndStatus += $" - Last online {lastSeenText}";
            }
            else
            {
                nameAndStatus += $" - {stateText}";
            }
        }

        return nameAndStatus;
    }
}
