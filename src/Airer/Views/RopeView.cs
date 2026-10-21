using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Airer.Views;

public sealed class RopeView : Control
{
    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width < 2)
            return;
        var sag = LineLayout.Sag(width);
        var top = LineLayout.RopeTop;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(-24, top), false);
            g.QuadraticBezierTo(new Point(width / 2, top + sag * 2), new Point(width + 24, top));
        }

        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        fade.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        fade.GradientStops.Add(new GradientStop(Color.FromRgb(90, 90, 90), 0.07));
        fade.GradientStops.Add(new GradientStop(Color.FromRgb(90, 90, 90), 0.93));
        fade.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 2.2), geo);
        context.DrawGeometry(null, new Pen(fade, 1.35), geo);
    }
}
