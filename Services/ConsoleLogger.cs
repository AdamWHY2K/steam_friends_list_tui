namespace SteamFriendsCLI.Services;

public class ConsoleLogger : ILogger
{
    public void LogError(string message, Exception? exception = null)
    {
        Console.WriteLine($"[ERROR] {message}");
        if (exception != null)
        {
            Console.WriteLine($"[ERROR] Exception: {exception.Message}");
            Console.WriteLine($"[ERROR] StackTrace: {exception.StackTrace}");
        }
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARNING] {message}");
    }

    public void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogDebug(string message)
    {
#if DEBUG
        Console.WriteLine($"[DEBUG] {message}");
#endif
    }
}
