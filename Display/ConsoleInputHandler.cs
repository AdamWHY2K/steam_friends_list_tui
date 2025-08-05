using SteamFriendsCLI.Services;
using SteamFriendsCLI.Constants;

namespace SteamFriendsCLI.Display;

public class ConsoleInputHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _inputTask;
    private volatile bool _isRunning;

    public event Action? ExitRequested;

    public ConsoleInputHandler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
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
        _inputTask?.Dispose();
    }
}
