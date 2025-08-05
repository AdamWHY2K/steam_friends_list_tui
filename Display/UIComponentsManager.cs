using Terminal.Gui;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

public class UIComponentsManager
{
    private readonly AppState _appState;
    private Window? _mainWindow;
    private ListView? _friendsListView;
    private FriendsListDataSource? _friendsDataSource;
    private Label? _statusLabel;
    private Label? _userLabel;

    public Window? MainWindow => _mainWindow;
    public ListView? FriendsListView => _friendsListView;
    public FriendsListDataSource? FriendsDataSource => _friendsDataSource;
    public Label? StatusLabel => _statusLabel;
    public Label? UserLabel => _userLabel;

    public UIComponentsManager(AppState appState)
    {
        _appState = appState;
    }

    public void InitializeComponents()
    {
        InitializeColors();
        CreateMainWindow();
        CreateUserInfoSection();
        CreateFriendsListSection();
        CreateStatusSection();
    }

    private void InitializeColors()
    {
        Colors.Base.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);
        Colors.Base.Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black);
        Colors.Base.HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black);
        Colors.Base.HotFocus = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black);
        Colors.Base.Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black);
    }

    private void CreateMainWindow()
    {
        _mainWindow = new Window("Steam Friends CLI")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
    }

    private void CreateUserInfoSection()
    {
        _userLabel = new Label("Loading user info...")
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 1
        };
        _mainWindow!.Add(_userLabel);
    }

    private void CreateFriendsListSection()
    {
        var friendsFrame = new FrameView("Friends")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _friendsDataSource = new FriendsListDataSource();
        
        _friendsListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            AllowsMultipleSelection = false,
            CanFocus = true
        };

        // Set our custom data source
        _friendsListView.Source = _friendsDataSource;

        friendsFrame.Add(_friendsListView);
        _mainWindow!.Add(friendsFrame);
    }

    private void CreateStatusSection()
    {
        _statusLabel = new Label("Connecting to Steam...")
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 2,
            Height = 1
        };
        _mainWindow!.Add(_statusLabel);
    }

    public void UpdateStatusLabel(int actualFriendCount, int blockedCount, int pendingCount)
    {
        var status = $"Friends: {actualFriendCount}";
        if (blockedCount > 0 || pendingCount > 0)
        {
            status += $" | Pending: {pendingCount} | Blocked: {blockedCount} | ";
        }
        _statusLabel!.Text = status;
    }

    public void UpdateUserInfo()
    {
        var userInfo = $" ❯ {_appState.CurrentPersonaName ?? "Loading..."}";
        
        var stateText = PersonaStateHelper.GetPersonaStateText(_appState.CurrentUserState);
        if (!string.IsNullOrEmpty(_appState.CurrentGame))
        {
            userInfo += $" - {_appState.CurrentGame}";
        }
        else
        {
            userInfo += $" - {stateText}";
        }
        _userLabel!.Text = userInfo;
        var colorScheme = PersonaColorHelper.CreatePersonaColorScheme(stateText);
        _userLabel.ColorScheme = colorScheme;
    }

    public void UpdateConnectionStatus(string status)
    {
        _statusLabel!.Text = status;
    }
}
