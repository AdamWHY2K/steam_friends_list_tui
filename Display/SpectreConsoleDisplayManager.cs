using Spectre.Console;
using SteamKit2;
using SteamFriendsCLI.Display.Components;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

/// <summary>
/// Refactored display manager that coordinates display components and state management
/// </summary>
public class SpectreConsoleDisplayManager : IFriendsDisplayManager
{
    private readonly ILogger _logger;
    private readonly ConsoleInputHandler _inputHandler;
    private readonly DisplayStateManager _stateManager;
    private readonly DisplayRenderer _renderer;
    private readonly HeaderComponent _headerComponent;
    private readonly FriendsListComponent _friendsListComponent;
    
    private bool _isInitialized = false;
    private volatile bool _isRunning = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    public event Action<uint>? AppInfoRequested;
    public event Action? ExitRequested;

    public SpectreConsoleDisplayManager(AppState appState, ILogger? logger = null)
    {
        _logger = logger ?? new ConsoleLogger();
        _inputHandler = new ConsoleInputHandler(_logger);
        
        // Create components
        _headerComponent = new HeaderComponent(appState, _logger);
        _friendsListComponent = new FriendsListComponent(_logger);
        _renderer = new DisplayRenderer(_headerComponent, _friendsListComponent, _logger);
        _stateManager = new DisplayStateManager(appState, _logger);
        
        // Wire up events
        _inputHandler.ExitRequested += () => ExitRequested?.Invoke();
        _inputHandler.ConsoleResized += OnConsoleResized;
        _inputHandler.ScrollUpRequested += OnScrollUpRequested;
        _inputHandler.ScrollDownRequested += OnScrollDownRequested;
        _inputHandler.ScrollToTopRequested += OnScrollToTopRequested;
        _inputHandler.ScrollToBottomRequested += OnScrollToBottomRequested;
        _stateManager.AppInfoRequested += (appId) => AppInfoRequested?.Invoke(appId);
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
        _stateManager.UpdateFromSteam(steamFriends);
        
        // Reset scroll position to top when friends list is updated
        _friendsListComponent.ScrollStateManager.Reset();
        
        RefreshDisplay();
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("UpdateConnectionStatus called before initialization");
            return;
        }

        _logger.LogInfo($"Connection status updated: {status}");
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("RefreshDisplay called before initialization");
            return;
        }

        _logger.LogDebug("Display refresh requested");
        
        // Update viewport size based on current console dimensions
        UpdateViewport();
        
        // Update components with current state
        var friends = _stateManager.GetCurrentFriends();
        var counts = _stateManager.GetCurrentCounts();
        
        _friendsListComponent.UpdateFriends(friends);
        _headerComponent.UpdateCounts(counts.friends, counts.blocked, counts.pending);
        
        // Render to console
        _renderer.RenderToConsole();
    }

    private void UpdateViewport()
    {
        try
        {
            int consoleHeight = Console.WindowHeight;
            _friendsListComponent.UpdateViewport(consoleHeight);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not update viewport: {ex.Message}");
            // Use default viewport size
            _friendsListComponent.UpdateViewport(25);
        }
    }

    private void OnScrollUpRequested()
    {
        if (_friendsListComponent.ScrollStateManager.ScrollUp())
        {
            RefreshDisplay();
        }
    }

    private void OnScrollDownRequested()
    {
        if (_friendsListComponent.ScrollStateManager.ScrollDown())
        {
            RefreshDisplay();
        }
    }

    private void OnScrollToTopRequested()
    {
        if (_friendsListComponent.ScrollStateManager.ScrollToTop())
        {
            RefreshDisplay();
        }
    }

    private void OnScrollToBottomRequested()
    {
        if (_friendsListComponent.ScrollStateManager.ScrollToBottom())
        {
            RefreshDisplay();
        }
    }

    private void OnConsoleResized()
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("OnConsoleResized called before initialization");
            return;
        }

        _logger.LogDebug("Console resize detected - refreshing display");
        
        // Reset scroll position to top on resize to avoid display issues
        _friendsListComponent.ScrollStateManager.Reset();
        
        RefreshDisplay();
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
            RefreshDisplay();
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

    public void Dispose()
    {
        try
        {
            Stop();
            
            // Unsubscribe from events
            if (_inputHandler != null)
            {
                _inputHandler.ExitRequested -= () => ExitRequested?.Invoke();
                _inputHandler.ConsoleResized -= OnConsoleResized;
                _inputHandler.ScrollUpRequested -= OnScrollUpRequested;
                _inputHandler.ScrollDownRequested -= OnScrollDownRequested;
                _inputHandler.ScrollToTopRequested -= OnScrollToTopRequested;
                _inputHandler.ScrollToBottomRequested -= OnScrollToBottomRequested;
            }
            
            _inputHandler?.Dispose();
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            _cancellationTokenSource?.Dispose();
            _logger.LogInfo("Display manager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disposing display manager", ex);
        }
    }
}
