namespace Airer;

/// <summary>
/// A watch folder is deleted from settings only when its volume is mounted and the
/// directory itself is gone. An offline mapped drive, or a path whose volume cannot
/// be checked, stays saved and is skipped until it can be watched again.
/// </summary>
public static class WatchFolderRules
{
    public enum VolumeState
    {
        Ready,
        NotReady,
        Unknown,
    }

    public static bool ShouldKeep(VolumeState volume, bool directoryExists)
        => volume != VolumeState.Ready || directoryExists;

    public static bool ShouldKeep(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        if (InspectVolume(path) != VolumeState.Ready)
            return true;
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static bool CanWatch(string path, out string reason)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "path is empty";
            return false;
        }

        if (InspectVolume(path) == VolumeState.NotReady)
        {
            reason = "drive is not ready";
            return false;
        }

        try
        {
            if (!Directory.Exists(path))
            {
                reason = "directory does not exist";
                return false;
            }
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }

        reason = "";
        return true;
    }

    public static VolumeState InspectVolume(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return VolumeState.Unknown;
        try
        {
            var full = Path.GetFullPath(path);
            if (IsUnc(full))
                return VolumeState.Unknown;
            if (!OperatingSystem.IsWindows() && IsMissingMacVolume(full))
                return VolumeState.NotReady;
            if (OperatingSystem.IsWindows())
                return WindowsVolume(full);
            return UnixMount(full);
        }
        catch (Exception)
        {
            return VolumeState.Unknown;
        }
    }

    private static bool IsUnc(string full)
        => full.StartsWith(@"\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal);

    private static VolumeState WindowsVolume(string full)
    {
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
            return VolumeState.Unknown;
        return new DriveInfo(root).IsReady ? VolumeState.Ready : VolumeState.NotReady;
    }

    private static VolumeState UnixMount(string full)
    {
        DriveInfo? best = null;
        var bestLength = -1;
        foreach (var drive in DriveInfo.GetDrives())
        {
            var name = drive.Name;
            if (string.IsNullOrEmpty(name) || !IsUnder(full, name) || name.Length <= bestLength)
                continue;
            best = drive;
            bestLength = name.Length;
        }

        if (best == null)
            return VolumeState.Unknown;
        try
        {
            return best.IsReady ? VolumeState.Ready : VolumeState.NotReady;
        }
        catch (Exception)
        {
            return VolumeState.Unknown;
        }
    }

    /// <summary>
    /// On macOS every absolute path has root "/", which is always ready. A disconnected
    /// disk is a missing /Volumes/&lt;name&gt; mount, not an unready DriveInfo root.
    /// </summary>
    private static bool IsMissingMacVolume(string full)
    {
        const string prefix = "/Volumes/";
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = full[prefix.Length..];
        var slash = rest.IndexOf('/');
        var name = slash < 0 ? rest : rest[..slash];
        if (name.Length == 0 || name is "." or "..")
            return false;
        var volume = prefix + name;
        foreach (var drive in DriveInfo.GetDrives())
        {
            var mount = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (mount.Equals(volume, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool IsUnder(string full, string mount)
    {
        var root = mount.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.Length == 0)
            return full.StartsWith('/');
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;
        return full.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
