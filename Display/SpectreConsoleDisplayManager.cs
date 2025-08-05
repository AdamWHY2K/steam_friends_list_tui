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
    private readonly ILogger _logger;
    private readonly ConsoleInputHandler _inputHandler;
    private readonly object _friendsListLock = new();
    
    private List<FriendInfo> _currentFriendsList = new();
    private (int friends, int blocked, int pending) _currentCounts = (0, 0, 0);
    private bool _isInitialized = false;
    private volatile bool _isRunning = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Event for requesting app info when game names are not cached
    public event Action<uint>? AppInfoRequested;
    
    // Event for requesting application exit
    public event Action? ExitRequested;

    public SpectreConsoleDisplayManager(AppState appState, ILogger? logger = null)
    {
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
        _friendsBuilder = new FriendsListBuilder(appState);
        _logger = logger ?? new ConsoleLogger();
        _inputHandler = new ConsoleInputHandler(_logger);
        
        // Wire up input handler events
        _inputHandler.ExitRequested += () => ExitRequested?.Invoke();
    }

    public void Initialize()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Display manager is already initialized");
            return;
        }

        try
        {
            // Try to disable mouse events to prevent input issues
            Console.TreatControlCAsInput = true;
            _logger.LogDebug("Console.TreatControlCAsInput set to true");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not set Console.TreatControlCAsInput: {ex.Message}");
        }

        try
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold cyan]Steam Friends List CLI[/] - Initializing interface...");
            _isInitialized = true;
            _logger.LogInfo("Display manager initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize display manager", ex);
            throw;
        }
    }

    public void DisplayFriendsList(SteamFriends? steamFriends)
    {
        if (steamFriends == null)
        {
            _logger.LogWarning("DisplayFriendsList called with null steamFriends");
            return;
        }
        
        _logger.LogDebug("DisplayFriendsList called - updating friends list");
        UpdateFriendsList(steamFriends);
        // Trigger immediate display update when friends list changes
        UpdateDisplay();
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("UpdateConnectionStatus called before initialization");
            return;
        }

        _logger.LogInfo($"Connection status updated: {status}");
        // Trigger immediate display update when connection status changes
        UpdateDisplay();
    }

    /// <summary>
    /// Triggers a display refresh. Call this when AppState data has changed 
    /// (e.g., user persona state, current game, etc.)
    /// </summary>
    public void RefreshDisplay()
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("RefreshDisplay called before initialization");
            return;
        }

        _logger.LogDebug("Display refresh requested");
        UpdateDisplay();
    }

    public void Run()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("SpectreConsoleDisplayManager must be initialized before running");

        if (_isRunning)
        {
            _logger.LogWarning("Display manager is already running");
            return;
        }

        _isRunning = true;
        _logger.LogInfo("Starting display manager");
        
        try
        {
            // Initial display
            UpdateDisplay();
            
            // Start the input handler
            _inputHandler.Start();
            
            _logger.LogInfo("Display manager started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start display manager", ex);
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Display manager is not running");
            return;
        }

        _logger.LogInfo("Stopping display manager");
        _isRunning = false;
        
        try
        {
            _cancellationTokenSource.Cancel();
            _inputHandler.Stop();
            
            AnsiConsole.Clear();
            _logger.LogInfo("Display manager stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error stopping display manager", ex);
        }
    }

    private void UpdateFriendsList(SteamFriends? steamFriends)
    {
        if (!_isInitialized || steamFriends == null)
        {
            _logger.LogWarning("UpdateFriendsList called with invalid state");
            return;
        }

        try
        {
            _currentCounts = _friendsBuilder.CountRelationships(steamFriends);
            List<FriendInfo> newFriendsList;

            if (_currentCounts.friends == 0)
            {
                newFriendsList = new List<FriendInfo>
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
            }
            else
            {
                var friendsList = _friendsBuilder.BuildFriendsList(steamFriends);
                _logger.LogDebug($"Built friends list with {friendsList.Count} friends");
                
                ProcessAppInfoRequests(steamFriends, friendsList);
                
                newFriendsList = _friendsBuilder.SortFriends(friendsList);
                _logger.LogDebug($"Sorted friends list with {newFriendsList.Count} friends");
            }

            // Thread-safe update of friends list
            lock (_friendsListLock)
            {
                _currentFriendsList = newFriendsList;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating friends list", ex);
            
            // Create error state friends list
            lock (_friendsListLock)
            {
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
    }

    private void ProcessAppInfoRequests(SteamFriends steamFriends, List<FriendInfo> friendsList)
    {
        try
        {
            SteamFriendsIterator.ForEachFriendOfType(steamFriends, EFriendRelationship.Friend, steamIdFriend =>
            {
                if (_friendsBuilder.NeedsAppInfoRequest(steamFriends, steamIdFriend, out uint appId))
                {
                    AppInfoRequested?.Invoke(appId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing app info requests", ex);
        }
    }

    private void UpdateDisplay()
    {
        if (!_isInitialized)
            return;

        try
        {
            AnsiConsole.Clear();
            
            // Create content with header info and friends list together
            var content = CreateCombinedContentPanel();

            // Wrap everything in the cyan border  
            var mainPanel = new Panel(content)
                .Header("[bold cyan]Steam Friends List CLI[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("cyan"))
                .Padding(1, 0);

            AnsiConsole.Write(mainPanel);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating display", ex);
        }
    }

    private Grid CreateCombinedContentPanel()
    {
        try
        {
            var grid = new Grid()
                .AddColumn()
                .AddRow(CreateHeaderSection())
                .AddRow(CreateFriendsSection());

            return grid;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating combined content panel", ex);
            return new Grid()
                .AddColumn()
                .AddRow(CreateErrorPanel("Error loading content"));
        }
    }

    private Markup CreateHeaderSection()
    {
        try
        {
            var userInfo = SpectreDisplayFormatter.FormatUserInfo(_appState);
            var countsInfo = $"Friends: [green]{_currentCounts.friends}[/]  Pending: [yellow]{_currentCounts.pending}[/]  Blocked: [red]{_currentCounts.blocked}[/]";
            var headerContent = $"{countsInfo}\n{userInfo}";
            return new Markup(headerContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating header section", ex);
            return new Markup("[red]Error loading user info[/]");
        }
    }

    private Panel CreateErrorPanel(string errorMessage)
    {
        return new Panel(new Markup($"[red]{errorMessage}[/]"))
            .Header("[bold red]Steam Friends List CLI[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("red"))
            .Padding(1, 0);
    }

    private Panel CreateHeaderPanel()
    {
        try
        {
            var userInfo = SpectreDisplayFormatter.FormatUserInfo(_appState);

            return new Panel(new Markup(userInfo))
                .Header("[bold cyan]Steam Friends List CLI[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("cyan"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating header panel", ex);
            return CreateErrorPanel("Error loading user info");
        }
    }
    private Grid CreateFriendsSection()
    {
        try
        {
            List<FriendInfo> currentFriends;
            lock (_friendsListLock)
            {
                currentFriends = new List<FriendInfo>(_currentFriendsList);
            }

            if (currentFriends.Count == 0)
            {
                return new Grid()
                    .AddColumn()
                    .AddRow(new Markup("[yellow]Loading friends list...[/]\n[dim]Please wait while Steam loads your friends data[/]"));
            }

            var table = new Table()
                .Border(TableBorder.None)
                .AddColumn(new TableColumn("Friend").NoWrap())
                .HideHeaders();

            var displayFriends = currentFriends.ToList();

            foreach (var friend in displayFriends)
            {
                var nameMarkup = SpectreDisplayFormatter.FormatFriendName(friend);
                var statusMarkup = SpectreDisplayFormatter.FormatFriendStatus(friend);

                // Add friend name and status on separate lines with indentation
                var friendDisplay = $"{nameMarkup}\n  {statusMarkup}";
                table.AddRow(friendDisplay);
            }

            // Add a separator line
            var content = new Grid()
                .AddColumn()
                .AddRow(new Rule().RuleStyle(Style.Parse("cyan")))
                .AddRow(table);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating friends section", ex);
            return new Grid()
                .AddColumn()
                .AddRow(CreateErrorPanel("Error loading friends list"));
        }
    }

    private Panel CreateFriendsPanel()
    {
        try
        {
            List<FriendInfo> currentFriends;
            lock (_friendsListLock)
            {
                currentFriends = new List<FriendInfo>(_currentFriendsList);
            }

            if (currentFriends.Count == 0)
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

            var displayFriends = currentFriends.ToList();
            
            if (displayFriends.Count == 0)
            {
                table.AddRow("[dim]No friends to display[/]", "");
            }
            else
            {
                foreach (var friend in displayFriends)
                {
                    var nameMarkup = SpectreDisplayFormatter.FormatFriendName(friend);
                    var statusMarkup = SpectreDisplayFormatter.FormatFriendStatus(friend);
                    table.AddRow(nameMarkup, statusMarkup);
                }
            }


            return new Panel(table)
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating friends panel", ex);
            return CreateErrorPanel("Error loading friends");
        }
    }

    public void Dispose()
    {
        try
        {
            Stop();
            _inputHandler?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _logger.LogInfo("Display manager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disposing display manager", ex);
        }
    }
}
