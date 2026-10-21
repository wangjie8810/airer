using Avalonia;
using System.Threading;

namespace Airer;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        if (!AcquireInstance())
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

    private static bool AcquireInstance()
    {
        try
        {
            _mutex = new Mutex(true, "Airer.SingleInstance", out var created);
            return created;
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
