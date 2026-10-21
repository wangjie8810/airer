using System.Runtime.InteropServices;

namespace Airer;

public static class ScreenGrab
{
    public static byte[]? Grab(int x, int y, int width, int height, double scale)
    {
        if (width < 1 || height < 1)
            return null;
        try
        {
            if (OperatingSystem.IsWindows())
                return GrabWindows(x, y, width, height);
            if (OperatingSystem.IsMacOS())
                return GrabMac(x, y, width, height, scale);
        }
        catch (Exception ex)
        {
            Log.Error("Capture failed: " + ex.Message);
        }
        return null;
    }

    private static byte[]? GrabWindows(int x, int y, int width, int height)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var previous = SelectObject(memory, bitmap);
        var copied = BitBlt(memory, 0, 0, width, height, screen, x, y, 0x00CC0020);
        var info = new BitmapInfo
        {
            Size = 40,
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
        };
        var pixels = new byte[width * height * 4];
        var rows = GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0);
        SelectObject(memory, previous);
        DeleteObject(bitmap);
        DeleteDC(memory);
        ReleaseDC(IntPtr.Zero, screen);
        if (!copied || rows == 0)
            return null;
        return ImageFiles.EncodeBgra(pixels, width, height);
    }

    private static byte[]? GrabMac(int x, int y, int width, int height, double scale)
    {
        if (scale < 0.5)
            scale = 1;
        var px = (int)Math.Round(x / scale);
        var py = (int)Math.Round(y / scale);
        var pw = Math.Max(1, (int)Math.Round(width / scale));
        var ph = Math.Max(1, (int)Math.Round(height / scale));
        var file = Path.Combine(Path.GetTempPath(), $"airer-{Guid.NewGuid():N}.png");
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                ArgumentList = { "-x", $"-R{px},{py},{pw},{ph}", file },
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (process == null || !process.WaitForExit(4000) || process.ExitCode != 0 || !File.Exists(file))
                return null;
            var bytes = File.ReadAllBytes(file);
            return bytes.Length > 32 ? bytes : null;
        }
        finally
        {
            try { File.Delete(file); } catch { /* temp cleanup */ }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);
}
