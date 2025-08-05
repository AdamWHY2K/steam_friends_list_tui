using System.Text;
using Spectre.Console;
using SteamKit2;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;
using SteamFriendsCLI.Constants;

namespace SteamFriendsCLI.Display;

public class SpectreConsoleDisplayManager : IFriendsDisplayManager
{
    private readonly AppState _appState;
    private readonly FriendsListBuilder _friendsBuilder;
    
    private List<FriendInfo> _currentFriendsList = new();
    private bool _isInitialized = false;
    private bool _isRunning = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Event for requesting app info when game names are not cached
    public event Action<uint>? AppInfoRequested;
    
    // Event for requesting application exit
    public event Action? ExitRequested;

    public SpectreConsoleDisplayManager(AppState appState)
    {
        _appState = appState;
        _friendsBuilder = new FriendsListBuilder(appState);
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        // Try to disable mouse events to prevent input issues
        try
        {
            Console.TreatControlCAsInput = true;
        }
        catch (Exception)
        {
            // Ignore if we can't set this
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]Steam Friends CLI[/] - Initializing interface...");
        _isInitialized = true;
    }

    public void DisplayFriendsList(SteamFriends? steamFriends)
    {
        if (steamFriends == null)
        {
            Console.WriteLine("DisplayFriendsList called with null steamFriends");
            return;
        }
        
        Console.WriteLine("DisplayFriendsList called - updating friends list");
        UpdateFriendsList(steamFriends);
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
            return;

        // Update status in the live display
        UpdateDisplay();
    }

    public void Run()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("SpectreConsoleDisplayManager must be initialized before running");

        _isRunning = true;
        
        // Initial display
        UpdateDisplay();
        
        // Start the UI update loop
        Task.Run(async () =>
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    UpdateDisplay();
                    await Task.Delay(1000, _cancellationTokenSource.Token); // Update every 1 second for more responsive updates
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Ignore display update errors to prevent crashes
                }
            }
        }, _cancellationTokenSource.Token);

        // Start the input handling loop in a blocking manner
        while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    // Only respond to specific key presses, ignore mouse and other input
                    if (keyInfo.Key == ConsoleKey.Q || 
                        keyInfo.Key == ConsoleKey.Escape ||
                        (keyInfo.KeyChar == 'q' || keyInfo.KeyChar == 'Q'))
                    {
                        Console.WriteLine("Exit key pressed - shutting down...");
                        ExitRequested?.Invoke();
                        break;
                    }
                    // Ignore all other input including mouse movements, function keys, etc.
                    // Only log if it's a printable character
                    else if (char.IsControl(keyInfo.KeyChar) == false)
                    {
                        // Silently ignore other printable characters
                    }
                }
                Thread.Sleep(100); // Check for input every 100ms
            }
            catch (Exception ex)
            {
                // Log input handling errors for debugging
                Console.WriteLine($"Input handling error: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold green]Steam Friends CLI[/] - Goodbye!");
    }

    private void UpdateFriendsList(SteamFriends? steamFriends)
    {
        if (!_isInitialized || steamFriends == null)
            return;

        try
        {
            var (actualFriendCount, blockedCount, pendingCount) = _friendsBuilder.CountRelationships(steamFriends);
            Console.WriteLine($"Friends count: {actualFriendCount}, Blocked: {blockedCount}, Pending: {pendingCount}");

            if (actualFriendCount == 0)
            {
                _currentFriendsList = new List<FriendInfo>
                {
                    new FriendInfo(
                        new SteamID(0), 
                        AppConstants.LoadingText.NoFriendsFound, 
                        EPersonaState.Offline, 
                        "", 
                        "", 
                        DateTime.MinValue
                    )
                };
                return;
            }

            var friendsList = _friendsBuilder.BuildFriendsList(steamFriends);
            Console.WriteLine($"Built friends list with {friendsList.Count} friends");
            
            ProcessAppInfoRequests(steamFriends, friendsList);
            
            var sortedFriends = _friendsBuilder.SortFriends(friendsList);
            _currentFriendsList = sortedFriends;
            Console.WriteLine($"Updated current friends list with {_currentFriendsList.Count} friends");
        }
        catch (Exception)
        {
            // If there's an error building the friends list, show a loading message
            _currentFriendsList = new List<FriendInfo>
            {
                new FriendInfo(
                    new SteamID(0), 
                    "Error loading friends list", 
                    EPersonaState.Offline, 
                    "", 
                    "", 
                    DateTime.MinValue
                )
            };
        }
    }

    private void ProcessAppInfoRequests(SteamFriends steamFriends, List<FriendInfo> friendsList)
    {
        SteamFriendsIterator.ForEachFriendOfType(steamFriends, EFriendRelationship.Friend, steamIdFriend =>
        {
            if (_friendsBuilder.NeedsAppInfoRequest(steamFriends, steamIdFriend, out uint appId))
            {
                AppInfoRequested?.Invoke(appId);
            }
        });
    }

    private void UpdateDisplay()
    {
        if (!_isInitialized)
            return;

        try
        {
            AnsiConsole.Clear();
            
            // Create the main layout
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(4),
                    new Layout("Content"),
                    new Layout("Footer").Size(2)
                );

            // Header section with user info
            var headerPanel = CreateHeaderPanel();
            layout["Header"].Update(headerPanel);

            // Content section with friends list
            var contentPanel = CreateFriendsPanel();
            layout["Content"].Update(contentPanel);

            // Footer section with controls
            var footerPanel = CreateFooterPanel();
            layout["Footer"].Update(footerPanel);

            AnsiConsole.Write(layout);
        }
        catch (Exception)
        {
            // Ignore display update errors to prevent crashes
        }
    }

    private Panel CreateHeaderPanel()
    {
        var userInfo = new StringBuilder();
        userInfo.Append($"❯ {_appState.CurrentPersonaName ?? "Loading..."}");
        
        var stateText = PersonaStateHelper.GetPersonaStateText(_appState.CurrentUserState);
        var stateColor = GetSpectreColorForPersonaState(_appState.CurrentUserState);
        
        if (!string.IsNullOrEmpty(_appState.CurrentGame))
        {
            userInfo.Append($" - [green]{_appState.CurrentGame}[/]");
        }
        else
        {
            userInfo.Append($" - [{stateColor}]{stateText}[/]");
        }

        return new Panel(new Markup(userInfo.ToString()))
            .Header("[bold cyan]Steam Friends CLI[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("cyan"));
    }

    private Panel CreateFriendsPanel()
    {
        if (_currentFriendsList == null || _currentFriendsList.Count == 0)
        {
            return new Panel(new Markup("[yellow]Loading friends list...[/]\n[dim]Please wait while Steam loads your friends data[/]"))
                .Header("[bold]Friends[/]")
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);
        }

        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn("Friend").NoWrap())
            .AddColumn(new TableColumn("Status"))
            .HideHeaders();

        var displayFriends = _currentFriendsList.Take(20).ToList(); // Limit to first 20 friends for display
        
        if (displayFriends.Count == 0)
        {
            table.AddRow("[dim]No friends to display[/]", "");
        }
        else
        {
            foreach (var friend in displayFriends)
            {
                var nameMarkup = FormatFriendName(friend);
                var statusMarkup = FormatFriendStatus(friend);
                table.AddRow(nameMarkup, statusMarkup);
            }
        }

        var friendsCount = _currentFriendsList.Count(f => f.SteamId.ConvertToUInt64() != 0);
        var header = friendsCount > 0 ? $"[bold]Friends ({friendsCount})[/]" : "[bold]Friends[/]";

        return new Panel(table)
            .Header(header)
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);
    }

    private Panel CreateFooterPanel()
    {
        return new Panel(new Markup("[dim]Press [bold yellow]Q[/] or [bold yellow]ESC[/] to quit[/]"))
            .Border(BoxBorder.None)
            .Padding(0, 0);
    }

    private string FormatFriendName(FriendInfo friend)
    {
        var color = GetSpectreColorForPersonaState(friend.State);
        if (!string.IsNullOrEmpty(friend.GameText))
        {
            color = "green"; // Playing games gets bright green
        }
        
        return $"[{color}]{friend.Name.EscapeMarkup()}[/]";
    }

    private string FormatFriendStatus(FriendInfo friend)
    {
        if (!string.IsNullOrEmpty(friend.GameText))
        {
            return $"[green]{friend.GameText.EscapeMarkup()}[/]";
        }
        
        var stateText = PersonaStateHelper.GetPersonaStateText(friend.State);
        var color = GetSpectreColorForPersonaState(friend.State);
        
        if (friend.State == EPersonaState.Offline && friend.LastSeen != DateTime.MinValue)
        {
            var lastSeenText = PersonaStateHelper.GetFormattedLastSeenText(friend.LastSeen);
            return $"[{color}]Last online {lastSeenText.EscapeMarkup()}[/]";
        }
        
        return $"[{color}]{stateText.EscapeMarkup()}[/]";
    }

    private string GetSpectreColorForPersonaState(EPersonaState state)
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

    public void Dispose()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
