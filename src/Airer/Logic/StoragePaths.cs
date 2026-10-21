namespace Airer;

public static class StoragePaths
{
    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Airer");

    public static string Inbox => Path.Combine(AppFolder, "Captures");

    public static string SettingsFile => Path.Combine(AppFolder, "settings.json");

    public static string LogFile => Path.Combine(AppFolder, "airer.log");

    public static string WindowsScreenshotsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Screenshots");

    public static string UniquePath(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);
        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var candidate = Path.Combine(folder, fileName);
        var n = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folder, $"{baseName} {n}{ext}");
            n++;
        }
        return candidate;
    }

    public static bool IsInside(string path, string folder)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(folder);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
