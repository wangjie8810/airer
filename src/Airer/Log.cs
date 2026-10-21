namespace Airer;

public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("info", message);

    public static void Error(string message) => Write("error", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}";
        Console.Error.WriteLine(line);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(StoragePaths.AppFolder);
                File.AppendAllText(StoragePaths.LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must not take the app down.
        }
    }
}
