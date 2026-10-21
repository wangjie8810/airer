namespace Airer;

public enum LineSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// Geometry of the clothesline. Photos hang from a sagging rope, centred,
/// and squeeze together when more of them fit than the chosen size allows.
/// </summary>
public static class LineLayout
{
    public const double RopeTop = 10;
    public const double PinAbove = 9.5;

    public static LineSize ParseSize(string? name) => name?.ToLowerInvariant() switch
    {
        "small" => LineSize.Small,
        "large" => LineSize.Large,
        _ => LineSize.Medium,
    };

    public static string SizeName(LineSize size) => size switch
    {
        LineSize.Small => "small",
        LineSize.Large => "large",
        _ => "medium",
    };

    public static double Scale(LineSize size) => size switch
    {
        LineSize.Small => 0.8,
        LineSize.Large => 1.35,
        _ => 1,
    };

    public static double PhotoMaxHeight(LineSize size) => Math.Round(104 * Scale(size));
    public static double CardWidth(LineSize size) => Math.Round(150 * Scale(size));
    public static double Spacing(LineSize size) => CardWidth(size) + 24;
    public static double PanelHeight(LineSize size) => PhotoMaxHeight(size) + 106;

    public static int VisibleCount(double width, LineSize size)
        => Math.Max(1, (int)Math.Floor((width - 80) / Spacing(size)));

    public static double Sag(double width) => Math.Min(30, width * 0.018);

    public static double RopeY(double x, double width)
    {
        if (width <= 0)
            return RopeTop;
        var f = x / width;
        return RopeTop + 4 * Sag(width) * f * (1 - f);
    }

    public static double ItemX(int index, int count, double width, int visible, double scroll, double spacing, bool keepScroll)
    {
        if (keepScroll && count > visible)
        {
            var newest = width / 2 + (visible - 1) * spacing / 2;
            return newest - (count - 1 - index) * spacing + scroll;
        }

        var gaps = Math.Max(count - 1, 0);
        var step = gaps > 0 ? Math.Min(spacing, Math.Max(0, width - 200) / gaps) : spacing;
        return width / 2 - gaps * step / 2.0 + index * step;
    }

    public static double MaxScroll(int count, int visible, double spacing)
        => Math.Max(0, count - visible) * spacing;

    public static (double W, double H) PhotoSize(double pixelW, double pixelH, LineSize size)
    {
        var maxW = CardWidth(size) - 14;
        var maxH = PhotoMaxHeight(size);
        if (pixelW <= 0 || pixelH <= 0)
            return (maxW, maxH);
        var scale = Math.Min(maxW / pixelW, maxH / pixelH);
        return (pixelW * scale, pixelH * scale);
    }

    public static double EdgeOpacity(double x, double width)
        => Math.Min(1, Math.Max(0, Math.Min(x, width - x) / 90));
}
