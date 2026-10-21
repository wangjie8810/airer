using System.Diagnostics;
using Microsoft.Win32;

namespace Airer;

public static class FileOps
{
    public static bool Recycle(string path)
    {
        try
        {
            if (!File.Exists(path))
                return true;
            if (OperatingSystem.IsWindows())
                return RecycleWindows(path);
            var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            Directory.CreateDirectory(trash);
            File.Move(path, StoragePaths.UniquePath(trash, Path.GetFileName(path)));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Recycle failed for {path}: {ex.Message}");
            return false;
        }
    }

    public static void Open(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void Edit(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mspaint.exe",
                ArgumentList = { path },
                UseShellExecute = true,
            });
            return;
        }
        Open(path);
    }

    public static void ShowInFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { "/select,", path },
                UseShellExecute = true,
            });
            return;
        }
        Process.Start("open", new[] { "-R", path });
    }

    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        else
            Process.Start("open", folder);
    }

    public static void SetOpenAtLogin(bool enabled)
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
                ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled)
                key.SetValue("Airer", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("Airer", throwOnMissingValue: false);
            return;
        }

        var agents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plist = Path.Combine(agents, "app.airer.plist");
        if (!enabled)
        {
            if (File.Exists(plist))
                File.Delete(plist);
            return;
        }
        Directory.CreateDirectory(agents);
        var exe = Environment.ProcessPath ?? "";
        File.WriteAllText(plist, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0"><dict>
            <key>Label</key><string>app.airer</string>
            <key>ProgramArguments</key><array><string>{System.Security.SecurityElement.Escape(exe)}</string></array>
            <key>RunAtLoad</key><true/>
            </dict></plist>
            """);
    }

    private static bool RecycleWindows(string path)
    {
        // SHFileOperation wants a double-null-terminated list. The marshaller adds one null.
        var op = new ShFileOp
        {
            Func = 3,
            From = path + "\0",
            Flags = 0x0040 | 0x0010 | 0x0004 | 0x0400,
        };
        return SHFileOperation(ref op) == 0;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct ShFileOp
    {
        public IntPtr Hwnd;
        public uint Func;
        public string? From;
        public string? To;
        public ushort Flags;
        public bool Aborted;
        public IntPtr NameMappings;
        public string? Title;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShFileOp op);
}
