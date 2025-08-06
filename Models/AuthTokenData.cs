namespace SteamFriendsCLI.Models;

public class AuthTokenData
{
    public string AccountName { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
}
