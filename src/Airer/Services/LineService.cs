using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace Airer;

public sealed class PeggedItem : INotifyPropertyChanged
{
    public PeggedItem(string path, Bitmap thumbnail, int pixelWidth, int pixelHeight, bool inInbox)
    {
        Path = path;
        Thumbnail = thumbnail;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        InInbox = inInbox;
        Tilt = Random.Shared.NextDouble() * 5 - 2.5;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string Path { get; private set; }
    public Bitmap Thumbnail { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public double Tilt { get; }
    public bool InInbox { get; private set; }
    public bool Falling { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void MarkFalling()
    {
        if (Falling)
            return;
        Falling = true;
        OnPropertyChanged(nameof(Falling));
    }

    public void Retarget(string path, bool inInbox)
    {
        Path = path;
        InInbox = inInbox;
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(InInbox));
    }

    public void ReplaceThumb(Bitmap thumbnail, int pixelWidth, int pixelHeight)
    {
        Thumbnail = thumbnail;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        OnPropertyChanged(nameof(Thumbnail));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LineService : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private Guid? _copiedId;
    private int _gust;
    private double _scroll;

    public LineService(AppSettings settings) => _settings = settings;

    public ObservableCollection<PeggedItem> Items { get; } = [];

    public Guid? CopiedId
    {
        get => _copiedId;
        private set => Set(ref _copiedId, value);
    }

    public int Gust
    {
        get => _gust;
        private set => Set(ref _gust, value);
    }

    public double Scroll
    {
        get => _scroll;
        private set => Set(ref _scroll, value);
    }

    public int LiveCount => Items.Count(i => !i.Falling);

    public IReadOnlyList<PeggedItem> Live => Items.Where(i => !i.Falling).ToList();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? HangingChanged;

    public int MaxItems(double width)
    {
        var visible = LineLayout.VisibleCount(width, _settings.Size);
        return _settings.KeepOnLine > 0 ? _settings.KeepOnLine : visible;
    }

    public PeggedItem? Hang(string path, int maxEdge, int maxItems, bool quietly)
    {
        if (Items.Any(i => !i.Falling && string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
            return null;
        var thumb = ImageFiles.Thumbnail(path, maxEdge);
        if (thumb == null)
            return null;
        var item = new PeggedItem(path, thumb, thumb.PixelSize.Width, thumb.PixelSize.Height, StoragePaths.IsInside(path, StoragePaths.Inbox));
        Items.Add(item);
        Scroll = 0;
        if (LiveCount > maxItems)
        {
            var oldest = Items.FirstOrDefault(i => !i.Falling && i.Id != item.Id);
            if (oldest != null)
                LetGo(oldest, quietly: true);
        }
        Remember();
        if (!quietly && _settings.Sound)
            Sounds.Tink();
        HangingChanged?.Invoke();
        return item;
    }

    public void Restore(int maxEdge)
    {
        foreach (var path in _settings.Hanging.ToList())
        {
            if (!File.Exists(path))
                continue;
            var thumb = ImageFiles.Thumbnail(path, maxEdge);
            if (thumb == null)
                continue;
            Items.Add(new PeggedItem(path, thumb, thumb.PixelSize.Width, thumb.PixelSize.Height, StoragePaths.IsInside(path, StoragePaths.Inbox)));
        }
        Remember();
    }

    public void ReloadThumbnails(int maxEdge)
    {
        foreach (var item in Items.Where(i => !i.Falling).ToList())
        {
            var thumb = ImageFiles.Thumbnail(item.Path, maxEdge);
            if (thumb != null)
                item.ReplaceThumb(thumb, thumb.PixelSize.Width, thumb.PixelSize.Height);
        }
    }

    public void Drop(PeggedItem item, bool quietly)
    {
        if (item.Falling || !Items.Contains(item))
            return;
        item.MarkFalling();
        Remember();
        if (!quietly && _settings.Sound)
            Sounds.Pop();
        HangingChanged?.Invoke();
    }

    public void Remove(PeggedItem item)
    {
        Items.Remove(item);
        Remember();
    }

    public bool Trash(PeggedItem item, bool quietly)
    {
        if (!FileOps.Recycle(item.Path))
        {
            if (!quietly)
                Sounds.Nope();
            return false;
        }
        Drop(item, quietly: true);
        return true;
    }

    public void Discard(PeggedItem item, bool quietly)
    {
        if (item.InInbox)
            Trash(item, quietly);
        else
            Drop(item, quietly);
    }

    public void LetGo(PeggedItem item, bool quietly)
    {
        if (item.InInbox && Trash(item, quietly: true))
            return;
        Drop(item, quietly);
    }

    public void Trim(int maxItems)
    {
        while (LiveCount > maxItems)
        {
            var oldest = Items.FirstOrDefault(i => !i.Falling);
            if (oldest == null)
                break;
            LetGo(oldest, quietly: true);
        }
        Scroll = 0;
    }

    public void ScrollBy(double delta, int count, int visible, double spacing)
    {
        if (_settings.KeepOnLine <= 0)
            return;
        var most = LineLayout.MaxScroll(count, visible, spacing);
        var next = Math.Min(most, Math.Max(0, Scroll + delta));
        if (Math.Abs(next - Scroll) > 0.1)
            Scroll = next;
    }

    public void MarkCopied(PeggedItem item)
    {
        CopiedId = item.Id;
        _ = ClearCopiedLater(item.Id);
    }

    public void Breeze()
    {
        if (LiveCount > 0)
            Gust++;
    }

    public void PruneMissing()
    {
        foreach (var item in Items.Where(i => !i.Falling && !File.Exists(i.Path)).ToList())
            Drop(item, quietly: true);
    }

    public bool SaveToDesktop(PeggedItem item)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var target = StoragePaths.UniquePath(desktop, Path.GetFileName(item.Path));
            File.Move(item.Path, target);
            Drop(item, quietly: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Save to desktop failed: " + ex.Message);
            Sounds.Nope();
            return false;
        }
    }

    public void Remember()
    {
        _settings.Hanging = Items.Where(i => !i.Falling).Select(i => i.Path).ToList();
    }

    private async Task ClearCopiedLater(Guid id)
    {
        await Task.Delay(1200);
        if (CopiedId == id)
            CopiedId = null;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
