using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsCLI.Display.Components;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

/// <summary>
/// Handles the overall layout and rendering of the display
/// </summary>
public class DisplayRenderer
{
    private readonly ILogger _logger;
    private readonly HeaderComponent _headerComponent;
    private readonly FriendsListComponent _friendsListComponent;

    public DisplayRenderer(HeaderComponent headerComponent, FriendsListComponent friendsListComponent, ILogger logger)
    {
        _headerComponent = headerComponent ?? throw new ArgumentNullException(nameof(headerComponent));
        _friendsListComponent = friendsListComponent ?? throw new ArgumentNullException(nameof(friendsListComponent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Renders the complete display to the console
    /// </summary>
    public void RenderToConsole()
    {
        try
        {
            AnsiConsole.Clear();
            
            var content = CreateMainLayout();
            var mainPanel = CreateMainPanel(content);
            
            AnsiConsole.Write(mainPanel);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error rendering to console", ex);
            RenderError("Failed to render display");
        }
    }

    private Grid CreateMainLayout()
    {
        return new Grid()
            .AddColumn()
            .AddRow(_headerComponent.Render())
            .AddRow(_friendsListComponent.Render());
    }

    private Panel CreateMainPanel(IRenderable content)
    {
        return new Panel(content)
            .Header("[bold cyan]Steam Friends List CLI[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("cyan"))
            .Padding(1, 0);
    }

    private void RenderError(string message)
    {
        try
        {
            AnsiConsole.Clear();
            var errorPanel = new Panel(new Markup($"[red]{message}[/]"))
                .Header("[bold red]Steam Friends List CLI - Error[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("red"))
                .Padding(1, 0);
            
            AnsiConsole.Write(errorPanel);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to render error message", ex);
        }
    }
}
