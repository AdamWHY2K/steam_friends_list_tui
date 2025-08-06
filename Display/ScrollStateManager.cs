using SteamFriendsCLI.Services;

namespace SteamFriendsCLI.Display;

/// <summary>
/// Manages the scroll position and viewport for the friends list
/// </summary>
public class ScrollStateManager
{
    private readonly ILogger _logger;
    private readonly object _scrollLock = new();
    
    private int _scrollPosition = 0;
    private int _totalItems = 0;
    private int _visibleItems = 0;

    public ScrollStateManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current scroll position
    /// </summary>
    public int ScrollPosition
    {
        get
        {
            lock (_scrollLock)
            {
                return _scrollPosition;
            }
        }
    }

    /// <summary>
    /// Updates the total number of items and visible items count
    /// </summary>
    public void UpdateItemCounts(int totalItems, int visibleItems, bool resetScroll = false)
    {
        lock (_scrollLock)
        {
            _totalItems = totalItems;
            _visibleItems = visibleItems;
            
            if (resetScroll)
            {
                _scrollPosition = 0;
            }
            
            // Ensure scroll position is still valid
            ClampScrollPosition();
        }
    }

    /// <summary>
    /// Scrolls up by the specified number of items
    /// </summary>
    public bool ScrollUp(int items = 1)
    {
        lock (_scrollLock)
        {
            int oldPosition = _scrollPosition;
            _scrollPosition = Math.Max(0, _scrollPosition - items);
            
            bool changed = oldPosition != _scrollPosition;
            if (changed)
            {
                _logger.LogDebug($"Scrolled up: {oldPosition} -> {_scrollPosition}");
            }
            return changed;
        }
    }

    /// <summary>
    /// Scrolls down by the specified number of items
    /// </summary>
    public bool ScrollDown(int items = 1)
    {
        lock (_scrollLock)
        {
            int oldPosition = _scrollPosition;
            int maxScroll = Math.Max(0, _totalItems - _visibleItems);
            _scrollPosition = Math.Min(maxScroll, _scrollPosition + items);
            
            bool changed = oldPosition != _scrollPosition;
            if (changed)
            {
                _logger.LogDebug($"Scrolled down: {oldPosition} -> {_scrollPosition}");
            }
            return changed;
        }
    }

    /// <summary>
    /// Scrolls to the top
    /// </summary>
    public bool ScrollToTop()
    {
        lock (_scrollLock)
        {
            int oldPosition = _scrollPosition;
            _scrollPosition = 0;
            
            bool changed = oldPosition != _scrollPosition;
            if (changed)
            {
                _logger.LogDebug($"Scrolled to top: {oldPosition} -> {_scrollPosition}");
            }
            return changed;
        }
    }

    /// <summary>
    /// Scrolls to the bottom
    /// </summary>
    public bool ScrollToBottom()
    {
        lock (_scrollLock)
        {
            int oldPosition = _scrollPosition;
            _scrollPosition = Math.Max(0, _totalItems - _visibleItems);
            
            bool changed = oldPosition != _scrollPosition;
            if (changed)
            {
                _logger.LogDebug($"Scrolled to bottom: {oldPosition} -> {_scrollPosition}");
            }
            return changed;
        }
    }

    /// <summary>
    /// Gets the range of items that should be visible
    /// </summary>
    public (int startIndex, int endIndex) GetVisibleRange()
    {
        lock (_scrollLock)
        {
            int startIndex = _scrollPosition;
            int endIndex = Math.Min(_totalItems, _scrollPosition + _visibleItems);
            return (startIndex, endIndex);
        }
    }

    /// <summary>
    /// Calculates the number of visible items based on console height and header size
    /// </summary>
    public static int CalculateVisibleItems(int consoleHeight, int headerLines)
    {
        const int panelHeader = 1;      // "Steam Friends List CLI" header
        const int panelBorders = 2;     // Top and bottom borders of the panel
        const int ruleLines = 1;        // Separator line between header and friends
        const int scrollIndicator = 1;  // "Showing X-Y of Z friends" line
        
        int reservedLines = panelHeader + panelBorders + headerLines + ruleLines + scrollIndicator;
        int availableLines = consoleHeight - reservedLines;
        
        // Each friend takes 2 lines (name + status)
        const int linesPerFriend = 2;
        int visibleItems = Math.Max(1, availableLines / linesPerFriend);
        return visibleItems;
    }

    /// <summary>
    /// Resets scroll position to top
    /// </summary>
    public void Reset()
    {
        lock (_scrollLock)
        {
            _scrollPosition = 0;
            _logger.LogDebug("Scroll position reset to top");
        }
    }

    private void ClampScrollPosition()
    {
        if (_totalItems <= _visibleItems)
        {
            _scrollPosition = 0;
        }
        else
        {
            int maxScroll = _totalItems - _visibleItems;
            _scrollPosition = Math.Max(0, Math.Min(_scrollPosition, maxScroll));
        }
    }
}
