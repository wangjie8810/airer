using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Airer.Views;

public partial class PegControl : UserControl
{
    private readonly AppHost _host = null!;
    private readonly PeggedItem _item = null!;
    private readonly RotateTransform _swing = new();
    private readonly TranslateTransform _drop = new();
    private Image Photo = null!;
    private TextBlock CopiedText = null!;
    private Border Close = null!;
    private Border PhotoFrame = null!;
    private Border Card = null!;
    private Border Copied = null!;
    private Point? _down;
    private PointerPressedEventArgs? _press;
    private bool _moved;
    private bool _long;
    private int _clickSerial;
    private DispatcherTimer? _hold;

    public PegControl()
    {
    }

    public PegControl(AppHost host, PeggedItem item)
    {
        InitializeComponent();
        _host = host;
        _item = item;
        RenderTransform = new TransformGroup { Children = { _swing, _drop } };
        RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        Photo.Source = item.Thumbnail;
        CopiedText.Text = Text.Copied;
        _swing.Angle = item.Tilt;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PeggedItem.Thumbnail))
                Photo.Source = item.Thumbnail;
            if (e.PropertyName == nameof(PeggedItem.Falling) && item.Falling)
                _ = PlayFall();
        };
        Close.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            CancelPress();
            _host.Discard(_item);
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        Photo = this.FindControl<Image>("Photo")!;
        CopiedText = this.FindControl<TextBlock>("CopiedText")!;
        Close = this.FindControl<Border>("Close")!;
        PhotoFrame = this.FindControl<Border>("PhotoFrame")!;
        Card = this.FindControl<Border>("Card")!;
        Copied = this.FindControl<Border>("Copied")!;
    }

    public void ApplyPhoto(double width, double height)
    {
        Photo.Width = width;
        Photo.Height = height;
        PhotoFrame.Width = width;
        PhotoFrame.Height = height;
    }

    public void SetHover(bool hover)
    {
        Close.IsVisible = hover && !_host.IsDragging;
        Card.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        Card.RenderTransform = new ScaleTransform(hover ? 1.03 : 1, hover ? 1.03 : 1);
    }

    public void SetCopied(bool copied) => Copied.IsVisible = copied;

    public bool CardContains(int x, int y)
    {
        if (!IsVisible || _item.Falling)
            return false;
        var origin = Card.PointToScreen(new Point(0, 0));
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var rect = new PixelRect(origin.X, origin.Y, (int)Math.Ceiling(Card.Bounds.Width * scale), (int)Math.Ceiling(Card.Bounds.Height * scale));
        return rect.Contains(new PixelPoint(x, y));
    }

    public void Arrive()
    {
        var start = _item.Tilt + 14;
        _swing.Angle = start;
        _ = Motion.Tween(value => _swing.Angle = value, start, _item.Tilt, 700, easeOut: true, CancellationToken.None);
    }

    public void Nudge()
    {
        var peak = _item.Tilt + (Random.Shared.NextDouble() * 2 + 1.4) * (Random.Shared.Next(2) == 0 ? -1 : 1);
        var from = _swing.Angle;
        _ = Motion.Tween(value => _swing.Angle = value, from, peak, 220, easeOut: true, CancellationToken.None)
            .ContinueWith(_ => Dispatcher.UIThread.Post(() =>
                _ = Motion.Tween(value => _swing.Angle = value, peak, _item.Tilt, 640, easeOut: true, CancellationToken.None)));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            OpenMenu();
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
            return;
        if (e.ClickCount >= 2)
        {
            CancelPress();
            _clickSerial++;
            _host.Open(_item);
            e.Handled = true;
            return;
        }
        _press = e;
        _down = e.GetPosition(this);
        _moved = false;
        _long = false;
        _hold?.Stop();
        _hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            if (_down == null || _moved)
                return;
            _long = true;
            _down = null;
            _host.Edit(_item);
        };
        _hold.Start();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_down is not { } start || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) < 6)
            return;
        _moved = true;
        CancelPress();
        _ = Drag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _hold?.Stop();
        e.Pointer.Capture(null);
        if (_down == null || _moved || _long)
        {
            _down = null;
            return;
        }
        _down = null;
        var serial = ++_clickSerial;
        _ = Task.Delay(320).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (_clickSerial == serial)
                _host.Copy(_item);
        }));
    }

    private async Task Drag(PointerEventArgs e)
    {
        _host.SetDragging(true);
        try
        {
            var transfer = new DataTransfer();
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            var file = storage == null ? null : await storage.TryGetFileFromPathAsync(_item.Path);
            if (file != null)
                transfer.Add(DataTransferItem.CreateFile(file));
            if (_press != null)
                await DragDrop.DoDragDropAsync(_press, transfer, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Log.Error("Drag failed: " + ex.Message);
        }
        finally
        {
            _host.SetDragging(false);
            _host.Prune();
        }
    }

    private void OpenMenu()
    {
        var menu = new ContextMenu();
        Add(menu, Text.Copy, () => _host.Copy(_item));
        Add(menu, Text.Open, () => _host.Open(_item));
        Add(menu, Text.Edit, () => _host.Edit(_item));
        Add(menu, Text.ShowInFolder, () => _host.ShowInFolder(_item));
        if (_item.InInbox)
            Add(menu, Text.SaveToDesktop, () => _host.SaveToDesktop(_item));
        menu.Items.Add(new Separator());
        Add(menu, _item.InInbox ? Text.Discard : Text.TakeDown, () => _host.Discard(_item));
        if (!_item.InInbox)
            Add(menu, Text.Trash, () => _host.Trash(_item));
        var live = _host.Line.Live;
        var index = live.ToList().FindIndex(i => i.Id == _item.Id);
        Add(menu, Text.TakeLeft, () => _host.TakeDown(_item, toTheLeft: true), index > 0);
        Add(menu, Text.TakeRight, () => _host.TakeDown(_item, toTheLeft: false), index >= 0 && index < live.Count - 1);
        menu.Closed += (_, _) => _host.SetMenu(false);
        _host.SetMenu(true);
        menu.Open(this);
    }

    private static void Add(ContextMenu menu, string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void CancelPress()
    {
        _hold?.Stop();
        _down = null;
    }

    private async Task PlayFall()
    {
        IsHitTestVisible = false;
        await Motion.Tween(value => Opacity = value, Opacity, 0, 180, easeOut: true, CancellationToken.None);
        await Motion.Tween(value => _drop.Y = value, 0, 180, 420, easeOut: true, CancellationToken.None);
        _host.FinishFall(_item);
    }
}
