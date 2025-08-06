using Spectre.Console;
using Spectre.Console.Rendering;
using SteamFriendsTUI.Services;

namespace SteamFriendsTUI.Display.Components;

/// <summary>
/// Base class for display components that can render themselves
/// </summary>
public abstract class DisplayComponent
{
    protected readonly ILogger _logger;

    protected DisplayComponent(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Renders the component and returns the renderable content
    /// </summary>
    public abstract IRenderable Render();

    /// <summary>
    /// Creates a standardized error panel
    /// </summary>
    protected static Panel CreateErrorPanel(string message)
    {
        return new Panel(new Markup($"[red]{message}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("red"))
            .Padding(1, 0);
    }
}
