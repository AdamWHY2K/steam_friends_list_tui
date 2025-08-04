using Terminal.Gui;

namespace SteamFriendsCLI.Display;

public class UIEventHandler
{
    public event Action? ExitRequested;

    public void SetupEventHandlers(UIComponentsManager uiManager)
    {
        if (uiManager.MainWindow != null)
        {
            uiManager.MainWindow.KeyPress += ProcessKeyPress;
            Application.Top.KeyPress += ProcessKeyPress;
        }

        if (uiManager.FriendsListView != null)
        {
            uiManager.FriendsListView.KeyPress += OnFriendsListKeyPress;
        }
    }

    public void CleanupEventHandlers(UIComponentsManager uiManager)
    {
        if (uiManager.MainWindow != null)
        {
            uiManager.MainWindow.KeyPress -= ProcessKeyPress;
        }
        
        if (uiManager.FriendsListView != null)
        {
            uiManager.FriendsListView.KeyPress -= OnFriendsListKeyPress;
        }
        
        Application.Top.KeyPress -= ProcessKeyPress;
    }

    private void ProcessKeyPress(View.KeyEventEventArgs e)
    {
        // Handle 'q' key to quit
        if (e.KeyEvent.Key == Key.q || e.KeyEvent.Key == Key.Q)
        {
            ExitRequested?.Invoke();
            e.Handled = true;
            return;
        }
    }

    private void OnFriendsListKeyPress(View.KeyEventEventArgs e)
    {
        // Handle our custom keys first, before the ListView processes them
        ProcessKeyPress(e);
    }
}
