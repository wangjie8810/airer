using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;

namespace Airer.Views;

public partial class SnipWindow : Window
{
    private Panel Root = null!;
    private Rectangle Band = null!;
    private Border SizeChip = null!;
    private TextBlock LabelText = null!;
    private TextBlock Help = null!;
    private readonly TaskCompletionSource<PixelRect?> _done = new();
    private Point? _start;
    private bool _finished;

    public SnipWindow()
    {
        InitializeComponent();
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Help.Text = Text.SnipHint;
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
    }

    public Task<PixelRect?> PickAsync(Screen screen)
    {
        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
        Width = screen.Bounds.Width / scale;
        Height = screen.Bounds.Height / scale;
        Show();
        Activate();
        Focus();
        return _done.Task;
    }

    public void Finish()
    {
        if (IsVisible)
            Close();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            Complete(null);
            return;
        }
        _start = e.GetPosition(Root);
        Help.IsVisible = false;
        Band.IsVisible = true;
        Update(e.GetPosition(Root));
        e.Pointer.Capture(Root);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_start != null)
            Update(e.GetPosition(Root));
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_start == null)
            return;
        var end = e.GetPosition(Root);
        var x = Math.Min(_start.Value.X, end.X);
        var y = Math.Min(_start.Value.Y, end.Y);
        var w = Math.Abs(end.X - _start.Value.X);
        var h = Math.Abs(end.Y - _start.Value.Y);
        _start = null;
        if (w < 8 || h < 8)
        {
            Complete(null);
            return;
        }
        var origin = this.PointToScreen(new Point(x, y));
        var far = this.PointToScreen(new Point(x + w, y + h));
        Opacity = 0;
        Complete(new PixelRect(origin.X, origin.Y, Math.Max(1, far.X - origin.X), Math.Max(1, far.Y - origin.Y)));
    }

    private void Update(Point end)
    {
        if (_start is not { } start)
            return;
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(Band, x);
        Canvas.SetTop(Band, y);
        Band.Width = w;
        Band.Height = h;
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        LabelText.Text = $"{Math.Round(w * scale)} × {Math.Round(h * scale)}";
        SizeChip.IsVisible = w > 8 && h > 8;
        Canvas.SetLeft(SizeChip, x);
        Canvas.SetTop(SizeChip, Math.Max(0, y - 28));
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        Root = this.FindControl<Panel>("Root")!;
        Band = this.FindControl<Rectangle>("Band")!;
        SizeChip = this.FindControl<Border>("SizeChip")!;
        LabelText = this.FindControl<TextBlock>("LabelText")!;
        Help = this.FindControl<TextBlock>("Help")!;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Complete(null);
        e.Handled = true;
    }

    private void Complete(PixelRect? rect)
    {
        if (_finished)
            return;
        _finished = true;
        _done.TrySetResult(rect);
    }
}
