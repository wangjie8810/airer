using System.Text.Json;

namespace Airer;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsStore(string? path = null) => Path = path ?? StoragePaths.SettingsFile;

    public string Path { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings();
            settings.Repair();
            return settings;
        }
        catch (Exception ex)
        {
            Log.Error("Could not read settings: " + ex.Message);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Repair();
        var folder = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, Path, overwrite: true);
    }
}
