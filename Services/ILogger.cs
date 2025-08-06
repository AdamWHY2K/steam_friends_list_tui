namespace SteamFriendsCLI.Services;

public interface ILogger
{
    void LogError(string message, Exception? exception = null);
    void LogWarning(string message);
    void LogInfo(string message);
    void LogDebug(string message);
}
