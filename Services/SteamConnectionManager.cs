namespace SteamFriendsTUI.Services;

public class SteamConnectionManager : IDisposable
{
    private readonly SteamClient _steamClient;
    private readonly AppState _appState;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _reconnectionTask;
    private bool _disposed = false;

    public event Action? Disconnected;
    public event Action? Reconnecting;
    public event Action? Reconnected;

    public SteamConnectionManager(SteamClient steamClient, AppState appState, ILogger logger)
    {
        _steamClient = steamClient ?? throw new ArgumentNullException(nameof(steamClient));
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void HandleConnected()
    {
        _logger.LogInfo("Connected to Steam");
        _appState.SetConnected(true);
        Reconnected?.Invoke();
    }

    public void HandleDisconnected(SteamClient.DisconnectedCallback callback)
    {
        _logger.LogInfo("Disconnected from Steam");
        
        _appState.SetConnected(false);
        Disconnected?.Invoke();

        if (_appState.IsRunning)
        {
            StartReconnectionTask();
        }
    }

    private void StartReconnectionTask()
    {
        if (_reconnectionTask != null && !_reconnectionTask.IsCompleted)
        {
            _logger.LogDebug("Reconnection task already running");
            return;
        }

        _reconnectionTask = Task.Run(async () => await AttemptReconnectionAsync(_cancellationTokenSource.Token));
    }

    private async Task AttemptReconnectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("Starting reconnection attempts");
        Reconnecting?.Invoke();

        while (!_appState.IsConnected && _appState.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Waiting before reconnection attempt");
                await Task.Delay(AppConstants.Timeouts.ReconnectionDelay, cancellationToken);

                if (!_appState.IsRunning || cancellationToken.IsCancellationRequested)
                    break;

                if (!_steamClient.IsConnected)
                {
                    _logger.LogDebug("Attempting to reconnect to Steam");
                    _steamClient.Connect();
                }

                _logger.LogDebug("Waiting for reconnection result");
                await Task.Delay(AppConstants.Timeouts.ReconnectionRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("Reconnection cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during reconnection attempt: {ex.Message}");
                try
                {
                    await Task.Delay(AppConstants.Timeouts.ReconnectionRetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogDebug("Reconnection task completed");
    }

    public void RequestDisconnect()
    {
        _logger.LogInfo("Disconnect requested");
        
        if (_steamClient.IsConnected)
        {
            _steamClient.Disconnect();
        }
    }

    public void Stop()
    {
        _logger.LogDebug("Stopping connection manager");
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        
        try
        {
            _reconnectionTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Ignore timeout exceptions during disposal
        }

        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
