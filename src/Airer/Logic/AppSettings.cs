namespace Airer;

public sealed class AppSettings
{
    public string LineSize { get; set; } = "medium";
    public int KeepOnLine { get; set; }
    public bool Sound { get; set; } = true;
    public bool OpenAtLogin { get; set; }
    public bool HangCopiedImages { get; set; }
    public bool RevealOnNew { get; set; } = true;
    public bool RevealOnTopEdge { get; set; } = true;
    public bool EmptyDismissed { get; set; }
    public bool Welcomed { get; set; }
    public List<string> WatchFolders { get; set; } = [];
    public List<string> Hanging { get; set; } = [];
    public HotkeyChord ToggleLine { get; set; } = HotkeyChord.ToggleDefault();
    public HotkeyChord RegionShot { get; set; } = HotkeyChord.RegionDefault();
    public HotkeyChord ScreenShot { get; set; } = HotkeyChord.ScreenDefault();

    public LineSize Size => LineLayout.ParseSize(LineSize);

    public void Repair()
    {
        WatchFolders ??= [];
        Hanging ??= [];
        ToggleLine ??= HotkeyChord.ToggleDefault();
        RegionShot ??= HotkeyChord.RegionDefault();
        ScreenShot ??= HotkeyChord.ScreenDefault();
        ToggleLine = RepairChord(ToggleLine, HotkeyChord.ToggleDefault());
        RegionShot = RepairChord(RegionShot, HotkeyChord.RegionDefault());
        ScreenShot = RepairChord(ScreenShot, HotkeyChord.ScreenDefault());
        if (KeepOnLine is not (0 or 25 or 50 or 100))
            KeepOnLine = 0;
        LineSize = LineLayout.SizeName(Size);
        WatchFolders = WatchFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static HotkeyChord RepairChord(HotkeyChord chord, HotkeyChord fallback)
        => chord.Key == "" || chord.IsValid ? chord : fallback;
}
