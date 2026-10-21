using Xunit;

namespace Airer.Tests;

public class WatchFolderTests
{
    [Theory]
    [InlineData(WatchFolderRules.VolumeState.NotReady, false, true)]
    [InlineData(WatchFolderRules.VolumeState.Unknown, false, true)]
    [InlineData(WatchFolderRules.VolumeState.Ready, false, false)]
    [InlineData(WatchFolderRules.VolumeState.Ready, true, true)]
    public void FolderStaysUnlessTheVolumeIsReadyAndTheDirectoryIsGone(
        WatchFolderRules.VolumeState volume, bool exists, bool keep)
    {
        Assert.Equal(keep, WatchFolderRules.ShouldKeep(volume, exists));
    }

    [Fact]
    public void BlankWatchFolderIsNotKept()
    {
        Assert.False(WatchFolderRules.ShouldKeep(" "));
    }

    [Fact]
    public void OfflineWatchFolderSurvivesSaveAndAMissingReadyFolderDoesNot()
    {
        var root = Path.Combine(Path.GetTempPath(), "airer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var present = Path.Combine(root, "present");
        Directory.CreateDirectory(present);
        var missing = Path.Combine(root, "airer-missing-folder");
        var offline = UnreadyWatchFolder();
        var hanging = Path.Combine(root, "missing.png");
        try
        {
            Assert.Equal(WatchFolderRules.VolumeState.Ready, WatchFolderRules.InspectVolume(present));
            Assert.Equal(WatchFolderRules.VolumeState.NotReady, WatchFolderRules.InspectVolume(offline));
            Assert.True(WatchFolderRules.CanWatch(present, out _));
            Assert.False(WatchFolderRules.CanWatch(offline, out var offlineReason));
            Assert.Contains("not ready", offlineReason, StringComparison.OrdinalIgnoreCase);
            Assert.False(WatchFolderRules.CanWatch(missing, out var missingReason));
            Assert.Contains("does not exist", missingReason, StringComparison.OrdinalIgnoreCase);

            var store = new SettingsStore(Path.Combine(root, "settings.json"));
            var settings = store.Load();
            settings.WatchFolders.Add(present);
            settings.WatchFolders.Add(present.ToUpperInvariant());
            settings.WatchFolders.Add(missing);
            settings.WatchFolders.Add(offline);
            settings.WatchFolders.Add(" ");
            settings.Hanging.Add(hanging);
            store.Save(settings);

            var saved = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(store.Path),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(saved);
            Assert.Single(saved.WatchFolders, f => string.Equals(f, present, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(saved.WatchFolders, f => string.Equals(f, offline, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(saved.WatchFolders, f => string.Equals(f, missing, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(saved.WatchFolders, string.IsNullOrWhiteSpace);
            Assert.Equal(new[] { hanging }, saved.Hanging);

            var loaded = store.Load();
            Assert.Contains(loaded.WatchFolders, f => string.Equals(f, offline, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(new[] { hanging }, loaded.Hanging);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string UnreadyWatchFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                        return Path.Combine(drive.Name, "AirerOffline", "shots");
                }
                catch (IOException)
                {
                    return Path.Combine(drive.Name, "AirerOffline", "shots");
                }
            }

            var used = new HashSet<char>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.Name.Length > 0 && char.IsLetter(drive.Name[0]))
                    used.Add(char.ToUpperInvariant(drive.Name[0]));
            }
            for (var letter = 'Z'; letter >= 'A'; letter--)
            {
                if (!used.Contains(letter))
                    return letter + @":\AirerOffline\shots";
            }
        }

        return "/Volumes/AirerOffline" + Guid.NewGuid().ToString("N") + "/shots";
    }
}
