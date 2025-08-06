using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsTUI.Display.Components;
using SteamFriendsTUI.Services;

namespace SteamFriendsTUI.Display;

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

            // Use a layout that fills the entire console height
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Main").Update(CreateMainPanel(content))
                );

            AnsiConsole.Write(layout);
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
            .Header("[white]Steam Friends List TUI[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("white"))
            .Padding(1, 0, 1, 0); // Left, Top, Right, Bottom padding
    }

    private void RenderError(string message)
    {
        try
        {
            AnsiConsole.Clear();
            var errorPanel = new Panel(new Markup($"[red]{message}[/]"))
                .Header("[bold red]Steam Friends List TUI - Error[/]")
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
