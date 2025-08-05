using System.Text;
using System.Linq;
using Terminal.Gui;
using SteamKit2;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;
using SteamFriendsCLI.Constants;

namespace SteamFriendsCLI.Display;

public class TerminalGuiDisplayManager : IFriendsDisplayManager
{
    private readonly AppState _appState;
    private readonly UIComponentsManager _uiManager;
    private readonly FriendsListBuilder _friendsBuilder;
    private readonly UIEventHandler _eventHandler;
    
    private List<FriendInfo> _currentFriendsList = new();
    private bool _isInitialized = false;
    
    // Event for requesting app info when game names are not cached
    public event Action<uint>? AppInfoRequested;
    
    // Event for requesting application exit
    public event Action? ExitRequested;

    public TerminalGuiDisplayManager(AppState appState)
    {
        _appState = appState;
        _uiManager = new UIComponentsManager(appState);
        _friendsBuilder = new FriendsListBuilder(appState);
        _eventHandler = new UIEventHandler();
        
        // Wire up event handlers
        _eventHandler.ExitRequested += OnExitRequested;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        Application.Init();
        _uiManager.InitializeComponents();
        _eventHandler.SetupEventHandlers(_uiManager);
        
        Application.Top.Width = Dim.Fill();
        Application.Top.Height = Dim.Fill();
        Application.Top.Add(_uiManager.MainWindow);
        
        _isInitialized = true;
    }

    public void DisplayFriendsList(SteamFriends? steamFriends)
    {
        UpdateFriendsList(steamFriends);
    }

    public void UpdateConnectionStatus(string status)
    {
        if (!_isInitialized)
            return;

        Application.MainLoop.Invoke(() =>
        {
            _uiManager.UpdateConnectionStatus(status);
        });
    }

    public void Run()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("TerminalGuiDisplayManager must be initialized before running");

        Application.Run();
    }

    public void Stop()
    {
        if (_isInitialized)
        {
            Application.RequestStop();
        }
    }

    private void UpdateFriendsList(SteamFriends? steamFriends)
    {
        if (!_isInitialized || steamFriends == null)
            return;

        Application.MainLoop.Invoke(() =>
        {
            var (actualFriendCount, blockedCount, pendingCount) = _friendsBuilder.CountRelationships(steamFriends);
            
            _uiManager.UpdateStatusLabel(actualFriendCount, blockedCount, pendingCount);
            _uiManager.UpdateUserInfo();

            if (actualFriendCount == 0)
            {
                DisplayNoFriends();
                return;
            }

            var friendsList = _friendsBuilder.BuildFriendsList(steamFriends);
            ProcessAppInfoRequests(steamFriends, friendsList);
            
            var sortedFriends = _friendsBuilder.SortFriends(friendsList);
            _currentFriendsList = sortedFriends;

            _uiManager.FriendsDataSource?.UpdateFriends(sortedFriends);
            _uiManager.FriendsListView?.SetNeedsDisplay();
        });
    }

    private void DisplayNoFriends()
    {
        var noFriendsInfo = new FriendInfo(
            new SteamID(0), 
            AppConstants.LoadingText.NoFriendsFound, 
            EPersonaState.Offline, 
            "", 
            "", 
            DateTime.MinValue
        );
        _uiManager.FriendsDataSource?.UpdateFriends(new List<FriendInfo> { noFriendsInfo });
        _uiManager.FriendsListView?.SetNeedsDisplay();
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

    private void OnExitRequested()
    {
        ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            _eventHandler.CleanupEventHandlers(_uiManager);
            _eventHandler.ExitRequested -= OnExitRequested;
            Application.Shutdown();
            _isInitialized = false;
        }
    }
}
