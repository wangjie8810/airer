using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;

namespace Airer.Views;

public partial class LineWindow : Window
{
    private Panel Root = null!;
    private TextBlock HintText = null!;
    private Border Hint = null!;
    private Canvas Cards = null!;
    private readonly AppHost _host = null!;
    private readonly Dictionary<Guid, PegControl> _pegs = [];
    private CancellationTokenSource? _slideCts;
    private bool _revealed;
    private int _homeY;
    private double _pixelY;
    private IntPtr _native;

    public LineWindow()
    {
    }

    public LineWindow(AppHost host)
    {
        InitializeComponent();
        _host = host;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        HintText.Text = Text.EmptyHint;
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += (_, _) => AttachNative();
        Closed += (_, _) =>
        {
            if (_native != IntPtr.Zero)
                WinHitTest.Detach(_native);
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        Root = this.FindControl<Panel>("Root")!;
        HintText = this.FindControl<TextBlock>("HintText")!;
        Hint = this.FindControl<Border>("Hint")!;
        Cards = this.FindControl<Canvas>("Cards")!;
    }

    public Screen? Screen { get; private set; }
    public string Key { get; private set; } = "";
    public bool Revealed => _revealed;

    public void Park()
    {
        Position = new PixelPoint(-32000, -32000);
        WinHitTest.SetPassThrough(_native, true);
    }

    public void Place(Screen screen)
    {
        Screen = screen;
        Key = $"{screen.Bounds.X}:{screen.Bounds.Y}:{screen.Bounds.Width}:{screen.Bounds.Height}";
        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        _homeY = screen.WorkingArea.Y;
        Width = screen.Bounds.Width / scale;
        Height = LineLayout.PanelHeight(_host.Settings.Size);
        if (_slideCts == null)
            _pixelY = _revealed ? _homeY : HiddenPixelY();
        ApplyY();
    }

    public void SetRevealed(bool revealed, bool instant = false)
    {
        // A screenshot must not wait out the 200ms retract, or the pegs are still on screen.
        var snapHide = instant && !revealed;
        if (_revealed == revealed && !snapHide)
            return;
        _revealed = revealed;
        if (snapHide)
            Jump(HiddenPixelY());
        else
            Slide(revealed ? _homeY : HiddenPixelY(), revealed);
        if (OperatingSystem.IsWindows())
            WinHitTest.SetPassThrough(_native, !revealed);
        if (OperatingSystem.IsMacOS() && !revealed)
            MacWindow.SetIgnoresMouse(_native, true);
    }

    public bool ContainsPanel(PixelPoint point)
    {
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        var rect = new PixelRect(Position.X, Position.Y, (int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
        return rect.Contains(point);
    }

    public bool Hit(int x, int y)
    {
        if (!_revealed)
            return false;
        return _pegs.Values.Any(peg => peg.CardContains(x, y));
    }

    public void UpdatePointer(PixelPoint point)
    {
        var over = false;
        foreach (var peg in _pegs.Values)
        {
            var hit = _revealed && peg.CardContains(point.X, point.Y);
            peg.SetHover(hit);
            over |= hit;
        }
        if (_native == IntPtr.Zero)
            return;
        if (OperatingSystem.IsWindows())
        {
            WinHitTest.Ensure(_native, Hit);
            WinHitTest.SetPassThrough(_native, !_revealed || !over);
        }
        else if (OperatingSystem.IsMacOS())
            MacWindow.SetIgnoresMouse(_native, !_revealed || !over);
    }

    public void Nudge()
    {
        if (_revealed)
        {
            foreach (var peg in _pegs.Values)
                peg.Nudge();
        }
    }

    public void Relayout()
    {
        var size = _host.Settings.Size;
        var width = Bounds.Width;
        if (width < 20)
            return;
        var items = _host.Line.Items.ToList();
        var visible = LineLayout.VisibleCount(width, size);
        var spacing = LineLayout.Spacing(size);
        var keep = _host.Settings.KeepOnLine > 0;
        var live = _host.Line.LiveCount;
        Hint.IsVisible = live == 0;
        Hint.Margin = new Thickness(0, LineLayout.RopeY(width / 2, width) + 28, 0, 0);
        var seen = new HashSet<Guid>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            seen.Add(item.Id);
            if (!_pegs.TryGetValue(item.Id, out var peg))
            {
                peg = new PegControl(_host, item);
                _pegs[item.Id] = peg;
                Cards.Children.Add(peg);
                peg.Arrive();
            }
            var x = LineLayout.ItemX(i, items.Count, width, visible, _host.Line.Scroll, spacing, keep);
            var ropeY = LineLayout.RopeY(x, width);
            var photo = LineLayout.PhotoSize(item.PixelWidth, item.PixelHeight, size);
            peg.ApplyPhoto(photo.W, photo.H);
            peg.Width = LineLayout.CardWidth(size);
            peg.Height = Math.Max(40, LineLayout.PanelHeight(size) - ropeY);
            Canvas.SetLeft(peg, x - peg.Width / 2);
            Canvas.SetTop(peg, ropeY - LineLayout.PinAbove);
            if (!item.Falling)
                peg.Opacity = LineLayout.EdgeOpacity(x, width);
            peg.IsHitTestVisible = !item.Falling;
            peg.SetCopied(_host.Line.CopiedId == item.Id);
        }
        foreach (var id in _pegs.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            Cards.Children.Remove(_pegs[id]);
            _pegs.Remove(id);
        }
    }

    private void AttachNative()
    {
        _native = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (_native == IntPtr.Zero)
            return;
        if (OperatingSystem.IsWindows())
            WinHitTest.Attach(_native, Hit);
        else if (OperatingSystem.IsMacOS())
            MacWindow.Configure(_native);
    }

    private double HiddenPixelY()
    {
        var scale = Screen?.Scaling > 0 ? Screen.Scaling : 1;
        var height = double.IsFinite(Height) && Height > 1 ? Height : LineLayout.PanelHeight(_host.Settings.Size);
        return _homeY - height * scale - 24;
    }

    private void Jump(double y)
    {
        _slideCts?.Cancel();
        _slideCts = null;
        _pixelY = y;
        ApplyY();
    }

    private void Slide(double target, bool show)
    {
        _slideCts?.Cancel();
        var cts = new CancellationTokenSource();
        _slideCts = cts;
        var from = _pixelY;
        _ = Motion.Tween(value =>
        {
            _pixelY = value;
            ApplyY();
        }, from, target, show ? 420 : 200, easeOut: show, cts.Token);
    }

    private void ApplyY()
    {
        if (Screen == null)
            return;
        Position = new PixelPoint(Screen.Bounds.X, (int)Math.Round(_pixelY));
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_revealed)
            return;
        _host.ScrollBy(-e.Delta.Y * 48);
        e.Handled = true;
    }
}
