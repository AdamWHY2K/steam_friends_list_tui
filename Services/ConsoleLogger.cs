namespace SteamFriendsTUI.Services;

public class ConsoleLogger : ILogger
{
    private static string GetTimestamp() => DateTime.Now.ToString("HH:mm:ss.fff");

    public void LogError(string message, Exception? exception = null)
    {
        if (DebugConfig.IsDebugMode)
        {
            Console.WriteLine($"[{GetTimestamp()}] [ERROR] {message}");
            if (exception != null)
            {
                Console.WriteLine($"[{GetTimestamp()}] [ERROR] Exception: {exception.Message}");
                Console.WriteLine($"[{GetTimestamp()}] [ERROR] StackTrace: {exception.StackTrace}");
            }
        }
    }

    public void LogWarning(string message)
    {
        if (DebugConfig.IsDebugMode)
        {
            Console.WriteLine($"[{GetTimestamp()}] [WARNING] {message}");
        }
    }

    public void LogInfo(string message)
    {
        if (DebugConfig.IsDebugMode)
        {
            Console.WriteLine($"[{GetTimestamp()}] [INFO] {message}");
        }
    }

    public void LogDebug(string message)
    {
        if (DebugConfig.IsDebugMode)
        {
            Console.WriteLine($"[{GetTimestamp()}] [DEBUG] {message}");
        }
    }
}
