using System.Linq;
using Spectre.Console;
using SteamFriendsTUI.Display.Components;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Services;
using SteamKit2;

namespace SteamFriendsTUI.Display;

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
    private readonly Timer _refreshTimer;

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

        // Create a timer to refresh the display every 60 seconds to update "last seen" times
        _refreshTimer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);

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
        // In debug mode, don't initialize the display interface
        if (SteamFriendsTUI.Services.DebugConfig.IsDebugMode)
        {
            _isInitialized = true; // Mark as initialized to avoid warnings
            return;
        }

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
            AnsiConsole.MarkupLine("[white]Steam Friends List TUI[/] - Initializing interface...");
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
        _refreshTimer.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        // Reset scroll position to top when friends list is updated
        _friendsListComponent.ScrollStateManager.Reset();

        RefreshDisplay(resetScroll: true);
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

    public void RefreshDisplay(bool resetScroll = false)
    {
        // In debug mode, don't show the friends list UI
        if (SteamFriendsTUI.Services.DebugConfig.IsDebugMode)
        {
            return;
        }

        if (!_isInitialized)
        {
            _logger.LogWarning("RefreshDisplay called before initialization");
            return;
        }

        _logger.LogDebug("Display refresh requested");

        // Update components with current state first
        var friends = _stateManager.GetCurrentFriends();
        var counts = _stateManager.GetCurrentCounts();

        _friendsListComponent.UpdateFriends(friends);
        _headerComponent.UpdateCounts(counts.friends, counts.blocked, counts.pending);

        // Update viewport size based on current console dimensions AFTER friends are updated
        UpdateViewport(resetScroll);

        // Render to console
        _renderer.RenderToConsole();
    }

    private void UpdateViewport(bool resetScroll = false)
    {
        try
        {
            int consoleHeight = Console.WindowHeight;
            _friendsListComponent.UpdateViewport(consoleHeight, resetScroll);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not update viewport: {ex.Message}");
            // Use default viewport size
            _friendsListComponent.UpdateViewport(25, resetScroll);
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

    private void OnTimerTick(object? state)
    {
        // In debug mode, we don't need to check _isRunning since we're not running the UI loop
        if (!_isInitialized || (!SteamFriendsTUI.Services.DebugConfig.IsDebugMode && !_isRunning))
            return;

        try
        {
            _logger.LogDebug("Timer tick: Checking for offline friends to refresh last seen times");
            var friends = _stateManager.GetCurrentFriends();
            bool hasOfflineFriends = friends.Any(f => f.State == EPersonaState.Offline && f.LastSeen != DateTime.MinValue);

            bool isDisconnected = !_stateManager.AppState.IsConnected;

            if (hasOfflineFriends || isDisconnected)
            {
                if (hasOfflineFriends)
                {
                    _logger.LogDebug("Timer tick: Found offline friends, refreshing display to update last seen times");

                    // Log some example friends and their times for debugging
                    var offlineFriendsWithTimes = friends.Where(f => f.State == EPersonaState.Offline && f.LastSeen != DateTime.MinValue).Take(3);
                    foreach (var friend in offlineFriendsWithTimes)
                    {
                        var timeDiff = DateTime.UtcNow - friend.LastSeen;
                        _logger.LogDebug($"Friend {friend.Name}: Last seen {friend.LastSeen:yyyy-MM-dd HH:mm:ss} UTC, diff: {timeDiff.TotalMinutes:F1} minutes");
                    }
                }

                if (isDisconnected)
                {
                    var timeText = _stateManager.AppState.GetTimeSinceDisconnection()?.TotalSeconds.ToString("F0") ?? "unknown";
                    _logger.LogDebug($"Timer tick: Currently disconnected for {timeText}s, refreshing display to update disconnection time");
                }

                RefreshDisplay();
            }
            else
            {
                _logger.LogDebug("Timer tick: No offline friends found, skipping refresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during timer refresh", ex);
        }
    }

    public void Run()
    {
        // In debug mode, don't show the friends list UI
        if (SteamFriendsTUI.Services.DebugConfig.IsDebugMode)
        {
            _logger.LogDebug("Debug mode is enabled - skipping friends list display");
            return;
        }

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
            _friendsListComponent.ScrollStateManager.Reset();
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
            _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
            _refreshTimer?.Dispose();
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
