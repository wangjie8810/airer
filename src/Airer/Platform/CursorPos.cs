using System.Runtime.InteropServices;

namespace Airer;

public readonly record struct CursorReading(double X, double Y, bool AlreadyPixels);

public static class CursorPos
{
    public static CursorReading? Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!GetCursorPos(out var point))
                    return null;
                return new CursorReading(point.X, point.Y, true);
            }

            if (OperatingSystem.IsMacOS())
            {
                var evt = CGEventCreate(IntPtr.Zero);
                var loc = CGEventGetLocation(evt);
                CFRelease(evt);
                return new CursorReading(loc.X, loc.Y, false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Cursor read failed: " + ex.Message);
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CorePoint
    {
        public double X;
        public double Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint point);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CorePoint CGEventGetLocation(IntPtr evt);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);
}
