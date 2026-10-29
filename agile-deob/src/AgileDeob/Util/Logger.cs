namespace AgileDeob.Util;

public enum LogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
}

/// <summary>
/// Dead-simple leveled console logger. No dependencies on purpose so the tool
/// stays easy to read for people picking apart CTF binaries.
/// </summary>
public sealed class Logger
{
    public LogLevel Level { get; set; } = LogLevel.Info;

    public Logger(LogLevel level = LogLevel.Info) => Level = level;

    public void Error(string msg) => Write(LogLevel.Error, "!!", ConsoleColor.Red, msg);
    public void Warn(string msg) => Write(LogLevel.Warn, " !", ConsoleColor.Yellow, msg);
    public void Info(string msg) => Write(LogLevel.Info, "  ", ConsoleColor.Gray, msg);
    public void Good(string msg) => Write(LogLevel.Info, " +", ConsoleColor.Green, msg);
    public void Debug(string msg) => Write(LogLevel.Debug, " .", ConsoleColor.DarkGray, msg);

    private void Write(LogLevel level, string tag, ConsoleColor color, string msg)
    {
        if (level > Level)
            return;

        ConsoleColor prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{tag}] {msg}");
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
