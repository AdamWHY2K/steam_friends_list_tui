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
    private bool _isInitialized = false;
    private volatile bool _isRunning = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _displayUpdateTask;
    
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
            AnsiConsole.MarkupLine("[bold cyan]Steam Friends CLI[/] - Initializing interface...");
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
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("UpdateConnectionStatus called before initialization");
            return;
        }

        _logger.LogInfo($"Connection status updated: {status}");
        // Trigger a display update to show the new status
        // Note: The status should be stored in AppState for display
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
            
            // Start the display update task
            _displayUpdateTask = StartDisplayUpdateLoop();
            
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

    private Task StartDisplayUpdateLoop()
    {
        return Task.Run(async () =>
        {
            _logger.LogDebug("Display update loop started");
            
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    UpdateDisplay();
                    await Task.Delay(AppConstants.Timeouts.DisplayUpdateInterval, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Display update loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in display update loop", ex);
                    // Continue the loop to prevent crashes
                }
            }
            
            _logger.LogDebug("Display update loop ended");
        }, _cancellationTokenSource.Token);
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
            
            // Wait for display update task to complete
            if (_displayUpdateTask != null)
            {
                _displayUpdateTask.Wait(AppConstants.Timeouts.GuiShutdown);
            }
            
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold green]Steam Friends CLI[/] - Goodbye!");
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
            var (actualFriendCount, blockedCount, pendingCount) = _friendsBuilder.CountRelationships(steamFriends);
            _logger.LogDebug($"Friends count: {actualFriendCount}, Blocked: {blockedCount}, Pending: {pendingCount}");

            List<FriendInfo> newFriendsList;

            if (actualFriendCount == 0)
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
            
            // Create the main layout
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(AppConstants.Display.HeaderSectionSize),
                    new Layout("Content"),
                    new Layout("Footer").Size(AppConstants.Display.FooterSectionSize)
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
        catch (Exception ex)
        {
            _logger.LogError("Error updating display", ex);
        }
    }

    private Panel CreateHeaderPanel()
    {
        try
        {
            var userInfo = SpectreDisplayFormatter.FormatUserInfo(_appState);

            return new Panel(new Markup(userInfo))
                .Header("[bold cyan]Steam Friends CLI[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("cyan"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating header panel", ex);
            return new Panel(new Markup("[red]Error loading user info[/]"))
                .Header("[bold cyan]Steam Friends CLI[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("cyan"));
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

            var displayFriends = currentFriends.Take(AppConstants.Display.MaxFriendsDisplayed).ToList();
            
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

            var friendsCount = currentFriends.Count(f => f.SteamId.ConvertToUInt64() != 0);
            var header = friendsCount > 0 ? $"[bold]Friends ({friendsCount})[/]" : "[bold]Friends[/]";

            return new Panel(table)
                .Header(header)
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating friends panel", ex);
            return new Panel(new Markup("[red]Error loading friends list[/]"))
                .Header("[bold]Friends[/]")
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);
        }
    }

    private Panel CreateFooterPanel()
    {
        return new Panel(new Markup("[dim]Press [bold yellow]Q[/] or [bold yellow]ESC[/] to quit[/]"))
            .Border(BoxBorder.None)
            .Padding(0, 0);
    }

    public void Dispose()
    {
        try
        {
            Stop();
            _inputHandler?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _displayUpdateTask?.Dispose();
            _logger.LogInfo("Display manager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disposing display manager", ex);
        }
    }
}
