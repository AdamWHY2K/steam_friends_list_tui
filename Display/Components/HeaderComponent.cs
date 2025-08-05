using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display.Components;

/// <summary>
/// Component responsible for rendering the header section with user info and counts
/// </summary>
public class HeaderComponent : DisplayComponent
{
    private readonly AppState _appState;
    private (int friends, int blocked, int pending) _counts;

    public HeaderComponent(AppState appState, ILogger logger) : base(logger)
    {
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
    }

    /// <summary>
    /// Updates the friend counts to display
    /// </summary>
    public void UpdateCounts(int friends, int blocked, int pending)
    {
        _counts = (friends, blocked, pending);
    }

    public override IRenderable Render()
    {
        try
        {
            var userInfo = SpectreDisplayFormatter.FormatUserInfo(_appState);
            var countsInfo = $"Friends: [green]{_counts.friends}[/]  Pending: [yellow]{_counts.pending}[/]  Blocked: [red]{_counts.blocked}[/]";
            var headerContent = $"{countsInfo}\n{userInfo}";
            
            return new Markup(headerContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating header section", ex);
            return new Markup("[red]Error loading user info[/]");
        }
    }
}
