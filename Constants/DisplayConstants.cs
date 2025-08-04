namespace SteamFriendsCLI.Constants;

public static class DisplayConstants
{
    // Box drawing constants
    public const int MAX_DISPLAY_WIDTH = 80;
    public const int BORDER_WIDTH = 2;
    public const int MAX_NAME_LENGTH = MAX_DISPLAY_WIDTH - BORDER_WIDTH - 2;
    public const int MAX_STATUS_LENGTH = MAX_DISPLAY_WIDTH - 6; // Account for indentation and borders
    public const string TRUNCATION_SUFFIX = "...";
    
    // Box drawing characters
    public const string BOX_TOP_LEFT = "╔";
    public const string BOX_TOP_RIGHT = "╗";
    public const string BOX_BOTTOM_LEFT = "╚";
    public const string BOX_BOTTOM_RIGHT = "╝";
    public const string BOX_HORIZONTAL = "═";
    public const string BOX_VERTICAL = "║";
    public const string BOX_T_DOWN = "╠";
    public const string BOX_T_UP = "╣";
    
    // Padding constants
    public const int NAME_LINE_TARGET_LENGTH = 79;
    public const int STATUS_LINE_TARGET_LENGTH = 81;
    public const string STATUS_INDENT = "   ";
    
    // Color codes
    public static class Colors
    {
        public const string RESET = "\u001b[0m";
        public const string GREEN = "\u001b[32m";
        public const string RED = "\u001b[31m";
        public const string YELLOW = "\u001b[33m";
        public const string MAGENTA = "\u001b[35m";
        public const string CYAN = "\u001b[36m";
        public const string BLUE = "\u001b[34m";
        public const string DARK_GRAY = "\u001b[90m";
        public const string WHITE = "\u001b[37m";
    }
}
