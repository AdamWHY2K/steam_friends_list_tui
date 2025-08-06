using SteamKit2;
using SteamKit2.Authentication;
using SteamFriendsTUI.Models;
using SteamFriendsTUI.Display;
using SteamFriendsTUI.Handlers;
using SteamFriendsTUI.Services;
using SteamFriendsTUI.Constants;

namespace SteamFriendsTUI;

public class SteamFriendsApp : IDisposable
{
    private readonly SteamClient _steamClient;
    private readonly CallbackManager _manager;
    private readonly SteamUser _steamUser;
    private readonly SteamFriends _steamFriends;
    private readonly SteamApps _steamApps;
    private readonly AppState _appState;
    private readonly IFriendsDisplayManager _displayManager;
    private readonly SteamCallbackHandler _callbackHandler;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _needsReAuthentication = false;

    public SteamFriendsApp()
    {
        _steamClient = new SteamClient();
        _manager = new CallbackManager(_steamClient);
        
        _steamUser = _steamClient.GetHandler<SteamUser>() ?? throw new InvalidOperationException("Failed to get SteamUser handler");
        _steamFriends = _steamClient.GetHandler<SteamFriends>() ?? throw new InvalidOperationException("Failed to get SteamFriends handler");
        _steamApps = _steamClient.GetHandler<SteamApps>() ?? throw new InvalidOperationException("Failed to get SteamApps handler");
        
        _appState = new AppState();
        var logger = new SteamFriendsTUI.Services.ConsoleLogger();
        _displayManager = new SpectreConsoleDisplayManager(_appState, logger);
        _callbackHandler = new SteamCallbackHandler(_steamClient, _steamUser, _steamFriends, _steamApps, _appState, _displayManager, logger);
        _cancellationTokenSource = new CancellationTokenSource();

        SubscribeToCallbacks();
        
        // Wire up the app info request event from display manager to callback handler
        _displayManager.AppInfoRequested += _callbackHandler.RequestAppInfo;
        
        // Wire up the exit request event from display manager
        _displayManager.ExitRequested += Stop;
        
        // Wire up authentication failure event
        _callbackHandler.AuthenticationFailed += OnAuthenticationFailed;
    }

    private void SubscribeToCallbacks()
    {
        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(_callbackHandler.OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(_callbackHandler.OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(_callbackHandler.OnLoggedOff);
        _manager.Subscribe<SteamUser.AccountInfoCallback>(_callbackHandler.OnAccountInfo);
        _manager.Subscribe<SteamUser.PlayingSessionStateCallback>(_callbackHandler.OnPlayingSession);
        _manager.Subscribe<SteamFriends.FriendsListCallback>(_callbackHandler.OnFriendsList);
        _manager.Subscribe<SteamFriends.PersonaStateCallback>(_callbackHandler.OnPersonaState);
        _manager.Subscribe<SteamApps.PICSProductInfoCallback>(_callbackHandler.OnPICSProductInfo);
    }

    public void Run()
    {
        try
        {
            // Show authentication status at startup
            Console.WriteLine("Steam Friends List TUI - Starting up...");
            Console.WriteLine(TokenStorage.GetTokenStatusMessage());
            Console.WriteLine();
            
            Console.WriteLine(AppConstants.Messages.ConnectingToSteam);
            _steamClient.Connect();

            // Wait for connection and authentication to complete before starting GUI
            while (!_appState.IsLoggedIn && _appState.IsRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _manager.RunWaitCallbacks(AppConstants.Timeouts.CallbackWait);
            }

            if (_appState.IsLoggedIn && _appState.IsRunning)
            {
                // Initialize and run Spectre.Console interface
                _displayManager.Initialize();
                _displayManager.UpdateConnectionStatus("Connected to Steam - Loading friends list...");
                
                // Give some time for initial friends list to load
                var friendsLoadTimeout = DateTime.Now.AddSeconds(10);
                while (!_appState.FriendsListReceived && DateTime.Now < friendsLoadTimeout && _appState.IsRunning)
                {
                    _manager.RunWaitCallbacks(AppConstants.Timeouts.CallbackWait);
                }
                
                // Display initial friends list if available
                if (_appState.FriendsListReceived)
                {
                    _displayManager.DisplayFriendsList(_steamFriends);
                }
                
                // Start the display manager
                _displayManager.Run();

                // Keep processing callbacks while running
                while (_appState.IsRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _manager.RunWaitCallbacks(AppConstants.Timeouts.CallbackWait);
                }

                _displayManager.Stop();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        finally
        {
            Dispose();
        }
    }

    private async Task OnConnectedAsync(SteamClient.ConnectedCallback callback)
    {
        try
        {
            // Try to use saved authentication tokens first
            var savedTokens = TokenStorage.LoadAuthTokens();
            
            if (savedTokens != null && !_needsReAuthentication)
            {
                Console.WriteLine($"Using saved authentication for '{savedTokens.AccountName}'...");
                Console.WriteLine("If authentication fails, a QR code will be displayed for re-authentication.");
                
                _steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = savedTokens.AccountName,
                    AccessToken = savedTokens.RefreshToken,
                });
                return;
            }

            // Fall back to QR code authentication
            await PerformQRCodeAuthentication();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            _appState.IsRunning = false;
        }
    }

    private async Task PerformQRCodeAuthentication()
    {
        Console.WriteLine();
        Console.WriteLine("=== QR Code Authentication Required ===");
        Console.WriteLine("Your authentication tokens are either missing, expired, or invalid.");
        Console.WriteLine("Please scan the QR code below with the Steam Mobile app to authenticate.");
        Console.WriteLine();
        
        var authSession = await _steamClient.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());

        authSession.ChallengeURLChanged = () =>
        {
            Console.WriteLine();
            Console.WriteLine(AppConstants.Messages.SteamRefreshChallenge);
            AuthenticationHelper.DrawQRCode(authSession);
        };

        AuthenticationHelper.DrawQRCode(authSession);

        var pollResponse = await authSession.PollingWaitForResultAsync();

        Console.WriteLine($"Successfully authenticated as '{pollResponse.AccountName}'!");
        Console.WriteLine("Authentication tokens have been saved for future use.");
        Console.WriteLine();

        // Save the authentication tokens for future use
        TokenStorage.SaveAuthTokens(pollResponse.AccountName, pollResponse.RefreshToken);

        _steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = pollResponse.AccountName,
            AccessToken = pollResponse.RefreshToken,
        });
        
        // Reset re-authentication flag since we successfully authenticated
        _needsReAuthentication = false;
    }

    private void OnAuthenticationFailed()
    {
        Console.WriteLine("Authentication failed. Will retry with QR code on next connection attempt...");
        _needsReAuthentication = true;
        
        // Disconnect and reconnect to trigger re-authentication
        if (_steamClient.IsConnected)
        {
            _steamClient.Disconnect();
        }
        
        // Wait a moment then reconnect
        Task.Delay(2000).ContinueWith(_ =>
        {
            if (_appState.IsRunning)
            {
                Console.WriteLine("Reconnecting for re-authentication...");
                _steamClient.Connect();
            }
        });
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        _ = Task.Run(async () => await OnConnectedAsync(callback));
    }

    public void Stop()
    {
        _appState.IsRunning = false;
        _cancellationTokenSource.Cancel();
        _displayManager.Stop();
    }

    public void Dispose()
    {
        _displayManager.AppInfoRequested -= _callbackHandler.RequestAppInfo;
        _displayManager.ExitRequested -= Stop;
        _callbackHandler.AuthenticationFailed -= OnAuthenticationFailed;
        _steamClient?.Disconnect();
        _displayManager?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
