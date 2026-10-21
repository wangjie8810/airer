using Xunit;

namespace Airer.Tests;

public class LineLayoutTests
{
    [Fact]
    public void RopeSagsInTheMiddleAndMeetsTheEnds()
    {
        const double width = 1400;
        var mid = LineLayout.RopeY(width / 2, width);
        var end = LineLayout.RopeY(0, width);
        Assert.True(mid > end);
        Assert.Equal(end, LineLayout.RopeY(width, width), 3);
        Assert.InRange(mid - end, 1, 30);
    }

    [Fact]
    public void OnePhotoHangsInTheCentre()
    {
        var x = LineLayout.ItemX(0, 1, 1200, 6, 0, 174, keepScroll: false);
        Assert.Equal(600, x, 0);
    }

    [Fact]
    public void ThreePhotosAreCentredAndEvenlySpaced()
    {
        var xs = Enumerable.Range(0, 3)
            .Select(i => LineLayout.ItemX(i, 3, 1200, 6, 0, 174, keepScroll: false))
            .ToArray();
        Assert.Equal(174, xs[1] - xs[0], 0);
        Assert.Equal(174, xs[2] - xs[1], 0);
        Assert.Equal(600, (xs[0] + xs[2]) / 2, 0);
    }

    [Fact]
    public void KeepModeScrollsOlderPhotosInFromTheLeft()
    {
        var newest = LineLayout.ItemX(4, 5, 1000, 3, 0, 100, keepScroll: true);
        var older = LineLayout.ItemX(0, 5, 1000, 3, 0, 100, keepScroll: true);
        Assert.True(newest > older);
        var scrolled = LineLayout.ItemX(0, 5, 1000, 3, 100, 100, keepScroll: true);
        Assert.Equal(100, scrolled - older, 0);
        Assert.Equal(200, LineLayout.MaxScroll(5, 3, 100));
    }

    [Fact]
    public void PhotoKeepsItsShapeInsideTheCard()
    {
        var wide = LineLayout.PhotoSize(1600, 900, LineSize.Medium);
        var tall = LineLayout.PhotoSize(900, 1600, LineSize.Medium);
        Assert.True(wide.W > wide.H);
        Assert.True(tall.H > tall.W);
        Assert.InRange(wide.H, 0, LineLayout.PhotoMaxHeight(LineSize.Medium) + 0.1);
        Assert.InRange(tall.W, 0, LineLayout.CardWidth(LineSize.Medium) - 14 + 0.1);
    }

    [Fact]
    public void PhotosFadeBeforeTheyLeaveTheScreen()
    {
        Assert.Equal(0, LineLayout.EdgeOpacity(0, 1000));
        Assert.Equal(1, LineLayout.EdgeOpacity(500, 1000));
        Assert.True(LineLayout.EdgeOpacity(40, 1000) < 1);
    }
}

public class HotkeyTests
{
    [Fact]
    public void RegionShortcutIsCtrlAltS()
    {
        var chord = HotkeyChord.RegionDefault();
        Assert.True(chord.Control);
        Assert.True(chord.Alt);
        Assert.Equal("S", chord.Key);
        Assert.Equal(0x53u, chord.WindowsVirtualKey());
        Assert.Contains("S", chord.Display());
    }

    [Fact]
    public void ScreenShortcutIsCtrlAlt3()
    {
        var chord = HotkeyChord.ScreenDefault();
        Assert.Equal("3", chord.Key);
        Assert.Equal(0x33u, chord.WindowsVirtualKey());
        Assert.Equal(0x14u, chord.MacKeyCode());
    }

    [Fact]
    public void BareLetterIsNotAGlobalShortcut()
    {
        Assert.Null(HotkeyChord.TryCreate(false, false, false, false, "S"));
        Assert.Null(HotkeyChord.TryCreate(false, false, true, false, "S"));
    }

    [Fact]
    public void EmptyChordMeansTheShortcutIsOff()
    {
        var settings = new AppSettings { ToggleLine = new HotkeyChord { Key = "" } };
        settings.Repair();
        Assert.Equal("", settings.ToggleLine.Key);
        Assert.False(settings.ToggleLine.IsValid);
    }

    [Fact]
    public void BrokenChordFallsBackToTheDefault()
    {
        var settings = new AppSettings { RegionShot = new HotkeyChord { Control = true, Alt = true, Key = "not a key" } };
        settings.Repair();
        Assert.Equal("S", settings.RegionShot.Key);
        Assert.True(settings.RegionShot.IsValid);
    }
}

public class StorageTests
{
    [Fact]
    public void UniquePathSkipsNamesThatAlreadyExist()
    {
        var folder = Path.Combine(Path.GetTempPath(), "airer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "shot.png"), "a");
            var second = StoragePaths.UniquePath(folder, "shot.png");
            Assert.Equal(Path.Combine(folder, "shot 2.png"), second);
            Assert.True(StoragePaths.IsInside(second, folder));
            Assert.False(StoragePaths.IsInside(folder, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var folder = Path.Combine(Path.GetTempPath(), "airer-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.KeepOnLine = 25;
        settings.RegionShot = HotkeyChord.TryCreate(true, true, true, false, "F6")!;
        settings.Hanging.Add(Path.Combine(folder, "missing.png"));
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(25, loaded.KeepOnLine);
        Assert.Equal("F6", loaded.RegionShot.Key);
        Assert.True(loaded.RegionShot.Shift);
        Assert.Single(loaded.Hanging);
        Directory.Delete(folder, recursive: true);
    }
}
