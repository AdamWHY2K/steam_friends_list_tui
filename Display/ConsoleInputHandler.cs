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
