using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Airer.Views;

namespace Airer;

public sealed class AppHost : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly List<LineWindow> _windows = [];
    private AppSettings _settings = new();
    private LineService _line = null!;
    private HotkeyHub _hotkeys = null!;
    private FolderWatch _watch = null!;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _tray;
    private Screens? _screens;
    private DispatcherTimer? _pointer;
    private DispatcherTimer? _clipboard;
    private DispatcherTimer? _gust;
    private string? _activeKey;
    private int _clipStamp;
    private bool _pinned;
    private bool _hover;
    private bool _menu;
    private bool _dragging;
    private bool _snipping;
    private bool _suppressHover;
    private bool _disposed;
    private DateTime _holdUntil;
    private DateTime _suppressUntil;
    private bool _seenOpen;

    public AppSettings Settings => _settings;
    public LineService Line => _line;
    public bool IsDragging => _dragging;

    public void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
        Directory.CreateDirectory(StoragePaths.Inbox);
        _settings = _store.Load();
        var args = desktop.Args ?? [];
        var demo = args.Contains("--demo");
        if (!_settings.Welcomed && OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(StoragePaths.WindowsScreenshotsFolder);
            if (_settings.WatchFolders.All(f => !string.Equals(f, StoragePaths.WindowsScreenshotsFolder, StringComparison.OrdinalIgnoreCase)))
                _settings.WatchFolders.Add(StoragePaths.WindowsScreenshotsFolder);
        }

        _line = new LineService(_settings);
        _line.Restore(ThumbEdge());
        _line.PropertyChanged += OnLineChanged;
        _line.Items.CollectionChanged += (_, _) =>
        {
            foreach (var window in _windows)
                window.Relayout();
            UpdateTip();
        };

        _hotkeys = new HotkeyHub(id => Dispatcher.UIThread.Post(() => Dispatch(id)));
        _hotkeys.Start();
        BindHotkeys();

        _watch = new FolderWatch();
        _watch.ImageArrived += path => Dispatcher.UIThread.Post(() => _ = HangPath(path, quietly: false));
        RestartWatch();

        _clipStamp = ClipboardImages.Stamp();
        _clipboard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _clipboard.Tick += (_, _) => PollClipboard();
        if (_settings.HangCopiedImages)
            _clipboard.Start();

        _pointer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _pointer.Tick += (_, _) => PollPointer();
        _pointer.Start();

        _gust = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        _gust.Tick += (_, _) =>
        {
            if (!_dragging)
                _line.Breeze();
            _gust.Interval = TimeSpan.FromSeconds(Random.Shared.Next(7, 16));
        };
        _gust.Start();

        BuildTray();
        Dispatcher.UIThread.Post(() => OpenWindows(demo, args.Contains("--debug")));

        if (demo)
            _ = LoadDemo();
        else if (!_settings.Welcomed)
        {
            _settings.Welcomed = true;
            Save();
            _ = new PromptWindow().ShowWelcome();
        }
    }

    public void Copy(PeggedItem item)
    {
        var png = ImageFiles.PngBytes(item.Path);
        if (png == null)
            return;
        ClipboardImages.SetPng(png);
        _line.MarkCopied(item);
        foreach (var window in _windows)
            window.Relayout();
    }

    public void Open(PeggedItem item)
    {
        try { FileOps.Open(item.Path); }
        catch (Exception ex) { Log.Error(ex.Message); }
    }

    public void Edit(PeggedItem item)
    {
        try { FileOps.Edit(item.Path); }
        catch (Exception ex) { Log.Error(ex.Message); }
    }

    public void ShowInFolder(PeggedItem item)
    {
        try { FileOps.ShowInFolder(item.Path); }
        catch (Exception ex) { Log.Error(ex.Message); }
    }

    public void SaveToDesktop(PeggedItem item) => _line.SaveToDesktop(item);

    public void Discard(PeggedItem item) => _line.Discard(item, quietly: false);

    public void Trash(PeggedItem item) => _line.Trash(item, quietly: false);

    public void FinishFall(PeggedItem item) => _line.Remove(item);

    public void TakeDown(PeggedItem item, bool toTheLeft)
    {
        var live = _line.Live.ToList();
        var index = live.FindIndex(i => i.Id == item.Id);
        if (index < 0)
            return;
        var side = toTheLeft ? live.Take(index).ToList() : live.Skip(index + 1).ToList();
        for (var i = 0; i < side.Count; i++)
        {
            var target = side[i];
            var quiet = i > 0;
            _ = Task.Delay(i * 60).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
            {
                if (!target.Falling)
                    _line.Discard(target, quiet);
            }));
        }
    }

    public void SetDragging(bool dragging) => _dragging = dragging;

    public void SetMenu(bool open) => _menu = open;

    public void Prune() => _line.PruneMissing();

    public void ScrollBy(double delta)
    {
        var window = ActiveWindow() ?? _windows.FirstOrDefault();
        if (window == null)
            return;
        var size = _settings.Size;
        _line.ScrollBy(delta, _line.Items.Count, LineLayout.VisibleCount(window.Bounds.Width, size), LineLayout.Spacing(size));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pointer?.Stop();
        _clipboard?.Stop();
        _gust?.Stop();
        _watch.Dispose();
        _hotkeys.Dispose();
        Save();
    }

    private void OpenWindows(bool demo, bool debug)
    {
        var window = new LineWindow(this);
        window.Park();
        window.Show();
        _screens = window.Screens;
        var primary = _screens.Primary ?? _screens.All.FirstOrDefault();
        if (primary != null)
            window.Place(primary);
        _windows.Add(window);
        _screens.Changed += (_, _) => Rebuild();
        Rebuild();
        if (demo)
        {
            _pinned = true;
            _activeKey ??= _windows.FirstOrDefault()?.Key;
            ApplyReveal();
        }
        if (debug)
            _ = Calibrate();
    }

    private void Rebuild()
    {
        if (_screens == null)
            return;
        var screens = _screens.All.ToList();
        foreach (var screen in screens)
        {
            var key = KeyOf(screen);
            var window = _windows.FirstOrDefault(w => w.Key == key);
            if (window == null)
            {
                window = new LineWindow(this) { Opacity = 0 };
                window.Park();
                window.Show();
                _windows.Add(window);
            }
            window.Place(screen);
            window.Opacity = 1;
            window.Relayout();
        }
        foreach (var window in _windows.Where(w => screens.All(s => KeyOf(s) != w.Key)).ToList())
        {
            _windows.Remove(window);
            window.Close();
        }
        ApplyReveal();
    }

    private async Task LoadDemo()
    {
        var wide = ImageFiles.WriteSample(StoragePaths.Inbox, "demo-wide.png", new SkiaSharp.SKColor(47, 111, 106), 1280, 720, "1");
        var tall = ImageFiles.WriteSample(StoragePaths.Inbox, "demo-tall.png", new SkiaSharp.SKColor(196, 107, 74), 640, 1100, "2");
        var square = ImageFiles.WriteSample(StoragePaths.Inbox, "demo-square.png", new SkiaSharp.SKColor(61, 90, 128), 900, 900, "3");
        foreach (var path in new[] { wide, tall, square })
            await _line.Hang(path, ThumbEdge(), 8, quietly: true);
        _settings.EmptyDismissed = false;
    }

    private void Dispatch(int id)
    {
        if (id == 1) Toggle();
        else if (id == 2) _ = RegionShot();
        else if (id == 3) _ = ScreenShot();
    }

    private void Toggle()
    {
        var showing = _windows.Any(w => w.Revealed);
        if (_pinned || showing)
        {
            _pinned = false;
            _hover = false;
            _suppressHover = true;
            _suppressUntil = DateTime.UtcNow.AddMilliseconds(400);
        }
        else
        {
            _pinned = true;
            _suppressHover = false;
            var screen = ScreenUnderCursor();
            if (screen != null)
                _activeKey = KeyOf(screen);
        }
        ApplyReveal();
    }

    private async Task RegionShot()
    {
        if (_snipping)
            return;
        var screen = ScreenUnderCursor() ?? _screens?.Primary;
        if (screen == null)
            return;
        _snipping = true;
        ApplyReveal();
        var overlay = new SnipWindow();
        var rect = await overlay.PickAsync(screen);
        overlay.Finish();
        if (rect is { Width: > 2, Height: > 2 } shot)
        {
            await Task.Delay(90);
            var png = ScreenGrab.Grab(shot.X, shot.Y, shot.Width, shot.Height, screen.Scaling);
            if (png == null)
                _ = new PromptWindow().ShowNote(Text.CaptureFailed);
            else
                await HangCapture(png, screen);
        }
        _snipping = false;
        ApplyReveal();
    }

    private async Task ScreenShot()
    {
        if (_snipping)
            return;
        var screen = ScreenUnderCursor() ?? _screens?.Primary;
        if (screen == null)
            return;
        _snipping = true;
        ApplyReveal();
        await Task.Delay(80);
        var bounds = screen.Bounds;
        var png = ScreenGrab.Grab(bounds.X, bounds.Y, bounds.Width, bounds.Height, screen.Scaling);
        _snipping = false;
        if (png == null)
            _ = new PromptWindow().ShowNote(Text.CaptureFailed);
        else
            await HangCapture(png, screen);
        ApplyReveal();
    }

    private async Task HangCapture(byte[] png, Screen screen)
    {
        var path = StoragePaths.UniquePath(StoragePaths.Inbox, $"Screenshot {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png");
        File.WriteAllBytes(path, png);
        _activeKey = KeyOf(screen);
        await HangPath(path, quietly: false);
    }

    private async Task HangPath(string path, bool quietly)
    {
        var window = ActiveWindow() ?? _windows.FirstOrDefault();
        var max = window == null ? 8 : _line.MaxItems(window.Bounds.Width);
        if (await _line.Hang(path, ThumbEdge(), max, quietly) == null)
            return;
        _settings.EmptyDismissed = false;
        if (_settings.RevealOnNew)
            _holdUntil = DateTime.UtcNow.AddSeconds(2.6);
        _suppressHover = false;
        Save();
        ApplyReveal();
    }

    private void PollClipboard()
    {
        if (!_settings.HangCopiedImages)
            return;
        var stamp = ClipboardImages.Stamp();
        if (stamp == _clipStamp)
            return;
        _clipStamp = stamp;
        if (ClipboardImages.Suppress > 0)
        {
            ClipboardImages.Suppress--;
            return;
        }
        var png = ClipboardImages.ReadPureImage();
        if (png == null)
            return;
        var path = StoragePaths.UniquePath(StoragePaths.Inbox, $"Clipboard {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png");
        File.WriteAllBytes(path, png);
        _ = HangPath(path, quietly: false);
    }

    private void PollPointer()
    {
        if (_screens == null || _snipping)
            return;
        var reading = CursorPos.Read();
        if (reading == null)
            return;
        var point = ToPixels(reading.Value);
        if (point == null)
            return;
        var screen = _screens.ScreenFromPoint(point.Value);
        var onTop = screen != null && InTopBand(screen, point.Value);
        var overPanel = false;
        foreach (var window in _windows)
        {
            window.UpdatePointer(point.Value);
            if (window.Revealed && window.ContainsPanel(point.Value))
                overPanel = true;
        }
        if (_suppressHover && !onTop && !overPanel)
            _suppressHover = false;
        if (_suppressHover && DateTime.UtcNow < _suppressUntil)
            onTop = false;

        var canTop = _settings.RevealOnTopEdge && (_line.LiveCount > 0 || !_settings.EmptyDismissed);
        _hover = (onTop && canTop) || overPanel || _menu || _dragging;
        if (screen != null && (_hover || _pinned || DateTime.UtcNow < _holdUntil))
            _activeKey = KeyOf(screen);
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        var show = !_snipping && (_pinned || _menu || _dragging || DateTime.UtcNow < _holdUntil || _hover);
        if (show && _activeKey == null)
            _activeKey = _windows.FirstOrDefault()?.Key;
        if (show)
            _seenOpen = true;
        foreach (var window in _windows)
            window.SetRevealed(show && window.Key == _activeKey, instant: _snipping);
        if (!show && _seenOpen && _line.LiveCount == 0 && !_settings.EmptyDismissed && _windows.Count > 0)
        {
            _settings.EmptyDismissed = true;
            Save();
        }
        UpdateTip();
    }

    private bool InTopBand(Screen screen, PixelPoint point)
    {
        if (point.X < screen.Bounds.X || point.X >= screen.Bounds.Right)
            return false;
        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var menu = Math.Max(0, screen.WorkingArea.Y - screen.Bounds.Y);
        var band = menu > 0 ? menu + (int)(4 * scale) : (int)(6 * scale);
        return point.Y >= screen.Bounds.Y && point.Y < screen.Bounds.Y + Math.Max(band, 4);
    }

    private PixelPoint? ToPixels(CursorReading reading)
    {
        if (reading.AlreadyPixels)
            return new PixelPoint((int)Math.Round(reading.X), (int)Math.Round(reading.Y));
        if (_screens == null)
            return null;
        foreach (var screen in _screens.All)
        {
            var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
            var left = screen.Bounds.X / scale;
            var top = screen.Bounds.Y / scale;
            var right = left + screen.Bounds.Width / scale;
            var bottom = top + screen.Bounds.Height / scale;
            if (reading.X < left || reading.X >= right || reading.Y < top || reading.Y >= bottom)
                continue;
            return new PixelPoint(
                screen.Bounds.X + (int)Math.Round((reading.X - left) * scale),
                screen.Bounds.Y + (int)Math.Round((reading.Y - top) * scale));
        }
        var primary = _screens.Primary;
        var fallback = primary == null || primary.Scaling <= 0 ? 1 : primary.Scaling;
        return new PixelPoint((int)Math.Round(reading.X * fallback), (int)Math.Round(reading.Y * fallback));
    }

    private Screen? ScreenUnderCursor()
    {
        var reading = CursorPos.Read();
        if (reading == null || _screens == null)
            return _screens?.Primary;
        var point = ToPixels(reading.Value);
        return point == null ? _screens.Primary : _screens.ScreenFromPoint(point.Value) ?? _screens.Primary;
    }

    private LineWindow? ActiveWindow() => _windows.FirstOrDefault(w => w.Key == _activeKey);

    private static string KeyOf(Screen screen)
        => $"{screen.Bounds.X}:{screen.Bounds.Y}:{screen.Bounds.Width}:{screen.Bounds.Height}";

    private int ThumbEdge() => (int)Math.Round(LineLayout.PhotoMaxHeight(_settings.Size) * 2.5);

    private void RestartWatch()
    {
        Directory.CreateDirectory(StoragePaths.Inbox);
        var folders = new List<string> { StoragePaths.Inbox };
        folders.AddRange(_settings.WatchFolders);
        _watch.Start(folders);
    }

    private void BindHotkeys()
    {
        _hotkeys.Replace(
        [
            (1, _settings.ToggleLine, (Action)(() => Dispatcher.UIThread.Post(() => Dispatch(1)))),
            (2, _settings.RegionShot, () => Dispatcher.UIThread.Post(() => Dispatch(2))),
            (3, _settings.ScreenShot, () => Dispatcher.UIThread.Post(() => Dispatch(3))),
        ]);
    }

    private void BuildTray()
    {
        _tray = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = Text.AppName,
            Menu = BuildMenu(),
        };
        _tray.Clicked += (_, _) => Toggle();
        var icons = new TrayIcons { _tray };
        TrayIcon.SetIcons(Avalonia.Application.Current!, icons);
    }

    private void RefreshMenu()
    {
        if (_tray != null)
            _tray.Menu = BuildMenu();
        UpdateTip();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        var showing = _windows.Any(w => w.Revealed);
        menu.Items.Add(Item(showing && _pinned ? Text.HideLine : Text.ShowLine, Toggle, HotkeyLabel(_settings.ToggleLine, 1)));
        menu.Items.Add(Item(Text.RegionShot, () => _ = RegionShot(), HotkeyLabel(_settings.RegionShot, 2)));
        menu.Items.Add(Item(Text.ScreenShot, () => _ = ScreenShot(), HotkeyLabel(_settings.ScreenShot, 3)));
        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(Sub(Text.Keep,
        [
            Check(Text.AsManyAsFit, _settings.KeepOnLine == 0, () => SetKeep(0)),
            Check(Text.Photos(25), _settings.KeepOnLine == 25, () => SetKeep(25)),
            Check(Text.Photos(50), _settings.KeepOnLine == 50, () => SetKeep(50)),
            Check(Text.Photos(100), _settings.KeepOnLine == 100, () => SetKeep(100)),
        ]));
        menu.Items.Add(Sub(Text.Size,
        [
            Check(Text.Small, _settings.Size == LineSize.Small, () => SetSize(LineSize.Small)),
            Check(Text.Medium, _settings.Size == LineSize.Medium, () => SetSize(LineSize.Medium)),
            Check(Text.Large, _settings.Size == LineSize.Large, () => SetSize(LineSize.Large)),
        ]));
        menu.Items.Add(Check(Text.Sounds, _settings.Sound, () => { _settings.Sound = !_settings.Sound; Save(); }));
        menu.Items.Add(Check(Text.HangCopied, _settings.HangCopiedImages, ToggleClipboard));
        menu.Items.Add(Check(Text.RevealOnNew, _settings.RevealOnNew, () => { _settings.RevealOnNew = !_settings.RevealOnNew; Save(); }));
        menu.Items.Add(Check(Text.RevealOnTop, _settings.RevealOnTopEdge, () => { _settings.RevealOnTopEdge = !_settings.RevealOnTopEdge; Save(); }));
        menu.Items.Add(new NativeMenuItemSeparator());

        var watchItems = _settings.WatchFolders.Select(folder => Item(folder, () =>
        {
            _settings.WatchFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
            RestartWatch();
            Save();
            RefreshMenu();
        })).Cast<NativeMenuItemBase>().ToList();
        watchItems.Add(Item(Text.AddFolder, () => _ = AddFolder()));
        menu.Items.Add(Sub(Text.Watch, watchItems));
        menu.Items.Add(Item(Text.OpenFolder, () => FileOps.OpenFolder(StoragePaths.Inbox)));
        menu.Items.Add(Item(Text.TakeAllDown, Clear));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Sub(Text.Shortcuts,
        [
            Item($"{Label(1)}    {Shown(_settings.ToggleLine)}", () => _ = ChangeChord(1)),
            Item($"{Label(2)}    {Shown(_settings.RegionShot)}", () => _ = ChangeChord(2)),
            Item($"{Label(3)}    {Shown(_settings.ScreenShot)}", () => _ = ChangeChord(3)),
        ]));
        menu.Items.Add(Check(Text.OpenAtLogin, _settings.OpenAtLogin, () =>
        {
            _settings.OpenAtLogin = !_settings.OpenAtLogin;
            try { FileOps.SetOpenAtLogin(_settings.OpenAtLogin); }
            catch (Exception ex) { Log.Error(ex.Message); _settings.OpenAtLogin = false; }
            Save();
        }));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(Text.Thanks, () => _ = new PromptWindow().ShowNote(Text.ThanksBody, Text.ThanksTitle)));
        menu.Items.Add(Item(Text.Quit, () => _desktop?.Shutdown()));
        return menu;
    }

    private async Task AddFolder()
    {
        var owner = ActiveWindow() ?? _windows.FirstOrDefault();
        if (owner == null)
            return;
        var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Text.AddFolderTitle,
            AllowMultiple = false,
        });
        var path = picked.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (_settings.WatchFolders.All(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            _settings.WatchFolders.Add(path);
        RestartWatch();
        Save();
        RefreshMenu();
    }

    private async Task ChangeChord(int id)
    {
        _hotkeys.Replace(Array.Empty<(int, HotkeyChord, Action)>());
        var pick = await new PromptWindow().PickChord(Label(id));
        if (pick.Accepted && pick.Chord != null)
        {
            if (id == 1) _settings.ToggleLine = pick.Chord;
            if (id == 2) _settings.RegionShot = pick.Chord;
            if (id == 3) _settings.ScreenShot = pick.Chord;
            Save();
        }
        BindHotkeys();
        RefreshMenu();
    }

    private void ToggleClipboard()
    {
        _settings.HangCopiedImages = !_settings.HangCopiedImages;
        _clipStamp = ClipboardImages.Stamp();
        if (_settings.HangCopiedImages) _clipboard?.Start();
        else _clipboard?.Stop();
        Save();
    }

    private void SetKeep(int count)
    {
        _settings.KeepOnLine = count;
        var window = ActiveWindow() ?? _windows.FirstOrDefault();
        _line.Trim(window == null ? count : _line.MaxItems(window.Bounds.Width));
        foreach (var open in _windows)
            open.Relayout();
        Save();
        RefreshMenu();
    }

    private void SetSize(LineSize size)
    {
        _settings.LineSize = LineLayout.SizeName(size);
        foreach (var window in _windows)
        {
            window.Height = LineLayout.PanelHeight(size);
            window.Relayout();
        }
        _line.ReloadThumbnails(ThumbEdge());
        var active = ActiveWindow() ?? _windows.FirstOrDefault();
        if (active != null)
            _line.Trim(_line.MaxItems(active.Bounds.Width));
        Save();
        RefreshMenu();
    }

    private void Clear()
    {
        var live = _line.Live.ToList();
        for (var i = 0; i < live.Count; i++)
        {
            var item = live[i];
            var quiet = i > 0;
            _ = Task.Delay(i * 60).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
            {
                if (!item.Falling)
                    _line.Discard(item, quiet);
            }));
        }
    }

    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LineService.Scroll) or nameof(LineService.CopiedId))
        {
            foreach (var window in _windows)
                window.Relayout();
        }
        else if (e.PropertyName == nameof(LineService.Gust))
        {
            foreach (var window in _windows)
                window.Nudge();
        }
    }

    private string HotkeyLabel(HotkeyChord chord, int id)
    {
        var text = Shown(chord);
        return _hotkeys.IsRegistered(id) || chord.Key == "" ? text : text + Text.BusyHotkey;
    }

    private static string Shown(HotkeyChord chord) => chord.Key == "" ? Text.None : chord.Display();

    private static string Label(int id) => id switch
    {
        1 => Text.ShowLine,
        2 => Text.RegionShot,
        _ => Text.ScreenShot,
    };

    private void UpdateTip()
    {
        if (_tray == null)
            return;
        var count = _line.LiveCount;
        _tray.ToolTipText = count == 0 ? Text.EmptyTip : Text.CountTip(count);
    }

    private void Save() => _store.Save(_settings);

    private async Task Calibrate()
    {
        await Task.Delay(700);
        var reading = CursorPos.Read();
        var window = _windows.FirstOrDefault();
        if (reading == null || window == null)
            return;
        Log.Info($"cursor raw=({reading.Value.X:0},{reading.Value.Y:0}) pixels={ToPixels(reading.Value)} window={window.Position} screen={window.PointToScreen(new Point(0, 0))} scale={window.RenderScaling}");
    }

    private static NativeMenuItem Item(string header, Action action, string? gesture = null)
    {
        var item = new NativeMenuItem(gesture == null ? header : $"{header}    {gesture}");
        item.Click += (_, _) => action();
        return item;
    }

    private static NativeMenuItem Check(string header, bool on, Action action)
    {
        var item = new NativeMenuItem(header)
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = on,
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static NativeMenuItem Sub(string header, IEnumerable<NativeMenuItemBase> children)
    {
        var menu = new NativeMenu();
        foreach (var child in children)
            menu.Items.Add(child);
        return new NativeMenuItem(header) { Menu = menu };
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Airer/Assets/tray.png"));
        return new WindowIcon(stream);
    }
}
