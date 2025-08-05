using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display.Components;

/// <summary>
/// Component responsible for rendering the friends list
/// </summary>
public class FriendsListComponent : DisplayComponent
{
    private readonly object _friendsLock = new();
    private List<FriendInfo> _friends = new();

    public FriendsListComponent(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// Updates the friends list to display
    /// </summary>
    public void UpdateFriends(List<FriendInfo> friends)
    {
        lock (_friendsLock)
        {
            _friends = friends?.ToList() ?? new List<FriendInfo>();
        }
    }

    public override IRenderable Render()
    {
        try
        {
            List<FriendInfo> currentFriends;
            lock (_friendsLock)
            {
                currentFriends = _friends.ToList();
            }

            if (currentFriends.Count == 0)
            {
                return CreateLoadingContent();
            }

            return CreateFriendsTable(currentFriends);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error rendering friends list", ex);
            return CreateErrorPanel("Error loading friends list");
        }
    }

    private IRenderable CreateLoadingContent()
    {
        return new Grid()
            .AddColumn()
            .AddRow(new Markup("[yellow]Loading friends list...[/]\n[dim]Please wait while Steam loads your friends data[/]"));
    }

    private IRenderable CreateFriendsTable(List<FriendInfo> friends)
    {
        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn("Friend").NoWrap())
            .HideHeaders();

        foreach (var friend in friends)
        {
            var nameMarkup = SpectreDisplayFormatter.FormatFriendName(friend);
            var statusMarkup = SpectreDisplayFormatter.FormatFriendStatus(friend);
            var friendDisplay = $"{nameMarkup}\n  {statusMarkup}";
            table.AddRow(friendDisplay);
        }

        return new Grid()
            .AddColumn()
            .AddRow(new Rule().RuleStyle(Style.Parse("cyan")))
            .AddRow(table);
    }
}
