namespace Airer;

public sealed class FolderWatch : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Timers.Timer _timer = new(250) { AutoReset = false };
    private readonly object _gate = new();
    private bool _started;

    public FolderWatch() => _timer.Elapsed += (_, _) => Flush();

    public event Action<string>? ImageArrived;

    public void Start(IEnumerable<string> folders)
    {
        StopWatchers();
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!WatchFolderRules.CanWatch(folder, out var reason))
            {
                Log.Info($"Skipping watch folder {folder}: {reason}.");
                continue;
            }
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                watcher.Created += (_, e) => Queue(e.FullPath);
                watcher.Renamed += (_, e) => Queue(e.FullPath);
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Log.Error($"Cannot watch {folder}: {ex.Message}");
            }
        }
        _started = true;
    }

    public void Dispose()
    {
        _timer.Dispose();
        StopWatchers();
    }

    private void Queue(string path)
    {
        if (!_started || !ImageFiles.IsImage(path))
            return;
        lock (_gate)
            _pending.Add(path);
        _timer.Stop();
        _timer.Start();
    }

    private void Flush()
    {
        string[] paths;
        lock (_gate)
        {
            paths = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var path in paths)
        {
            if (CanRead(path))
                ImageArrived?.Invoke(path);
        }
    }

    private static bool CanRead(string path)
    {
        for (var i = 0; i < 12; i++)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0)
                {
                    Thread.Sleep(80);
                    continue;
                }
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return stream.Length > 0 && stream.Length <= ImageFiles.MaxBytes;
            }
            catch (IOException)
            {
                Thread.Sleep(80);
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}
