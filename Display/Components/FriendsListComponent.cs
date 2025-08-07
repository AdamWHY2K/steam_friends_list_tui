using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsTUI.Constants;
using SteamFriendsTUI.Display;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;

namespace SteamFriendsTUI.Display.Components;

/// <summary>
/// Component responsible for rendering the friends list with scrolling support
/// </summary>
public class FriendsListComponent : DisplayComponent
{
    private readonly object _friendsLock = new();
    private List<FriendInfo> _friends = new();
    private readonly ScrollStateManager _scrollStateManager;

    public FriendsListComponent(ILogger logger) : base(logger)
    {
        _scrollStateManager = new ScrollStateManager(logger);
    }

    /// <summary>
    /// Gets the scroll state manager for this component
    /// </summary>
    public ScrollStateManager ScrollStateManager => _scrollStateManager;

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

    /// <summary>
    /// Updates the viewport size based on console dimensions
    /// </summary>
    public void UpdateViewport(int consoleHeight, bool resetScroll = false)
    {
        const int headerContentLines = 3; // Friend counts + username + status lines
        int visibleItems = ScrollStateManager.CalculateVisibleItems(consoleHeight, headerContentLines);

        lock (_friendsLock)
        {
            _scrollStateManager.UpdateItemCounts(_friends.Count, visibleItems, resetScroll);
        }

        _logger.LogDebug($"Updated viewport: console height={consoleHeight}, visible items={visibleItems}, total friends={_friends.Count}");
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
            .AddRow(new Markup($"[yellow]{AppConstants.LoadingText.Generic}[/]"));
    }

    private IRenderable CreateFriendsTable(List<FriendInfo> friends)
    {
        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn("Friend").NoWrap())
            .HideHeaders();

        // Get the visible range based on scroll position
        var (startIndex, endIndex) = _scrollStateManager.GetVisibleRange();

        // Only render friends in the visible range
        for (int i = startIndex; i < endIndex && i < friends.Count; i++)
        {
            var friend = friends[i];
            var nameMarkup = SpectreDisplayFormatter.FormatFriendName(friend);
            var statusMarkup = SpectreDisplayFormatter.FormatFriendStatus(friend);
            var friendDisplay = $"{nameMarkup}\n  {statusMarkup}";
            table.AddRow(friendDisplay);
        }

        // Create scroll indicator if needed
        var scrollInfo = CreateScrollIndicator(friends.Count);

        var grid = new Grid()
            .AddColumn()
            .AddRow(new Rule().RuleStyle(Style.Parse("white")))
            .AddRow(table);

        if (!string.IsNullOrEmpty(scrollInfo))
        {
            grid.AddRow(new Markup(scrollInfo));
        }
        return grid;
    }

    /// <summary>
    /// Creates a scroll indicator showing current position
    /// </summary>
    private string CreateScrollIndicator(int totalFriends)
    {
        var (startIndex, endIndex) = _scrollStateManager.GetVisibleRange();

        if (totalFriends == 0)
        {
            return ""; // No friends to show
        }

        // Always show the indicator if there are friends, even if all fit on screen
        int currentPosition = startIndex + 1; // 1-based for display
        int endPosition = Math.Min(endIndex, totalFriends);
        string text = SpectreDisplayFormatter.TruncateText(
            $"Showing {currentPosition}-{endPosition} of {totalFriends} friends",
            Console.WindowWidth - AppConstants.Display.IndicatorWidthReduction
        );
        return $"[dim]{text}[/]";
    }
}
