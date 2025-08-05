using SteamFriendsCLI.Services;
using SteamFriendsCLI.Constants;

namespace SteamFriendsCLI.Display;

public class ConsoleInputHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _inputTask;
    private volatile bool _isRunning;
    private int _lastWindowWidth;
    private int _lastWindowHeight;

    public event Action? ExitRequested;
    public event Action? ConsoleResized;
    public event Action? ScrollUpRequested;
    public event Action? ScrollDownRequested;
    public event Action? ScrollToTopRequested;
    public event Action? ScrollToBottomRequested;

    public ConsoleInputHandler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Initialize console dimensions
        try
        {
            _lastWindowWidth = Console.WindowWidth;
            _lastWindowHeight = Console.WindowHeight;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not get initial console dimensions: {ex.Message}");
            _lastWindowWidth = 80; // Default fallback
            _lastWindowHeight = 25; // Default fallback
        }
    }

    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogWarning("Input handler is already running");
            return;
        }

        _isRunning = true;
        _inputTask = Task.Run(ProcessInputAsync, _cancellationTokenSource.Token);
        _logger.LogDebug("Input handler started");
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _cancellationTokenSource.Cancel();
        
        try
        {
            _inputTask?.Wait(AppConstants.Timeouts.GuiShutdown);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error stopping input handler", ex);
        }
        
        _logger.LogDebug("Input handler stopped");
    }

    private async Task ProcessInputAsync()
    {
        _logger.LogDebug("Input processing loop started");
        
        while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Check for console resize
                CheckForConsoleResize();
                
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    HandleKeyInput(keyInfo);
                }
                
                await Task.Delay(AppConstants.Timeouts.InputCheckInterval, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in input processing loop", ex);
                // Continue processing to avoid crashing the input handler
            }
        }
        
        _logger.LogDebug("Input processing loop ended");
    }

    private void CheckForConsoleResize()
    {
        try
        {
            int currentWidth = Console.WindowWidth;
            int currentHeight = Console.WindowHeight;
            
            if (currentWidth != _lastWindowWidth || currentHeight != _lastWindowHeight)
            {
                _logger.LogDebug($"Console resize detected: {_lastWindowWidth}x{_lastWindowHeight} -> {currentWidth}x{currentHeight}");
                _lastWindowWidth = currentWidth;
                _lastWindowHeight = currentHeight;
                
                // Notify about the resize
                ConsoleResized?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error checking console dimensions: {ex.Message}");
        }
    }

    private void HandleKeyInput(ConsoleKeyInfo keyInfo)
    {
        // Handle exit keys
        if (keyInfo.Key == ConsoleKey.Q || 
            keyInfo.Key == ConsoleKey.Escape ||
            (keyInfo.KeyChar == 'q' || keyInfo.KeyChar == 'Q'))
        {
            _logger.LogInfo("Exit key pressed - requesting shutdown");
            ExitRequested?.Invoke();
            return;
        }

        // Handle scroll keys
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                _logger.LogDebug("Up arrow pressed - scrolling up");
                ScrollUpRequested?.Invoke();
                break;
                
            case ConsoleKey.DownArrow:
                _logger.LogDebug("Down arrow pressed - scrolling down");
                ScrollDownRequested?.Invoke();
                break;
                
            case ConsoleKey.Home:
                _logger.LogDebug("Home key pressed - scrolling to top");
                ScrollToTopRequested?.Invoke();
                break;
                
            case ConsoleKey.End:
                _logger.LogDebug("End key pressed - scrolling to bottom");
                ScrollToBottomRequested?.Invoke();
                break;
                
            case ConsoleKey.PageUp:
                _logger.LogDebug("Page up pressed - scrolling up by page");
                for (int i = 0; i < 5; i++) // Scroll up by 5 items
                    ScrollUpRequested?.Invoke();
                break;
                
            case ConsoleKey.PageDown:
                _logger.LogDebug("Page down pressed - scrolling down by page");
                for (int i = 0; i < 5; i++) // Scroll down by 5 items
                    ScrollDownRequested?.Invoke();
                break;
        }

        // Log other printable characters for debugging (but don't act on them)
        if (!char.IsControl(keyInfo.KeyChar))
        {
            _logger.LogDebug($"Ignored printable character: '{keyInfo.KeyChar}'");
        }
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
        if (_inputTask != null && 
            (_inputTask.Status == TaskStatus.RanToCompletion ||
             _inputTask.Status == TaskStatus.Faulted ||
             _inputTask.Status == TaskStatus.Canceled))
        {
            _inputTask.Dispose();
        }
    }
}
