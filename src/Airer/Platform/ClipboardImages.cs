using System.Runtime.InteropServices;

namespace Airer;

public static class ClipboardImages
{
    public static int Suppress;

    public static int Stamp()
    {
        if (OperatingSystem.IsWindows())
            return unchecked((int)GetClipboardSequenceNumber());
        if (OperatingSystem.IsMacOS())
            return unchecked((int)MacChangeCount());
        return 0;
    }

    public static void SetPng(byte[] png)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                SetWindows(png);
            else if (OperatingSystem.IsMacOS())
                SetMac(png);
            Suppress = 2;
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard write failed: " + ex.Message);
        }
    }

    public static byte[]? ReadPureImage()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ReadWindows();
            if (OperatingSystem.IsMacOS())
                return ReadMac();
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard read failed: " + ex.Message);
        }
        return null;
    }

    private static byte[]? ReadWindows()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;
        try
        {
            if (IsClipboardFormatAvailable(13) || IsClipboardFormatAvailable(15))
                return null;
            var pngFormat = RegisterClipboardFormat("PNG");
            if (pngFormat != 0 && IsClipboardFormatAvailable(pngFormat))
            {
                var png = CopyHandle(GetClipboardData(pngFormat));
                if (png is { Length: > 8 })
                    return png;
            }
            if (!IsClipboardFormatAvailable(8))
                return null;
            return DibToPng(CopyHandle(GetClipboardData(8)));
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void SetWindows(byte[] png)
    {
        var dib = PngToDib(png);
        if (!OpenClipboard(IntPtr.Zero))
            return;
        try
        {
            EmptyClipboard();
            SetHandle(RegisterClipboardFormat("PNG"), png);
            if (dib != null)
                SetHandle(8, dib);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void SetHandle(uint format, byte[] bytes)
    {
        var mem = GlobalAlloc(0x0002, (nuint)bytes.Length);
        var ptr = GlobalLock(mem);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        GlobalUnlock(mem);
        if (SetClipboardData(format, mem) == IntPtr.Zero)
            GlobalFree(mem);
    }

    private static byte[]? CopyHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return null;
        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero)
            return null;
        try
        {
            var size = (int)GlobalSize(handle);
            if (size <= 0 || size > ImageFiles.MaxBytes)
                return null;
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static byte[]? DibToPng(byte[]? dib)
    {
        if (dib == null || dib.Length < 40)
            return null;
        var header = BitConverter.ToInt32(dib, 0);
        var width = BitConverter.ToInt32(dib, 4);
        var heightSigned = BitConverter.ToInt32(dib, 8);
        var bpp = BitConverter.ToInt16(dib, 14);
        var compression = BitConverter.ToInt32(dib, 16);
        if (width <= 0 || bpp is not (24 or 32) || header < 40)
            return null;
        var bottomUp = heightSigned > 0;
        var height = Math.Abs(heightSigned);
        var offset = header + (compression == 3 ? 12 : 0);
        var stride = ((width * (bpp / 8) + 3) / 4) * 4;
        var bgra = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var srcRow = offset + (bottomUp ? height - 1 - y : y) * stride;
            for (var x = 0; x < width; x++)
            {
                var src = srcRow + x * (bpp / 8);
                var dst = (y * width + x) * 4;
                if (src + 2 >= dib.Length)
                    return null;
                bgra[dst] = dib[src];
                bgra[dst + 1] = dib[src + 1];
                bgra[dst + 2] = dib[src + 2];
                var alpha = bpp == 32 && src + 3 < dib.Length ? dib[src + 3] : (byte)255;
                bgra[dst + 3] = alpha == 0 ? (byte)255 : alpha;
            }
        }
        return ImageFiles.EncodeBgra(bgra, width, height);
    }

    private static byte[]? PngToDib(byte[] png)
    {
        using var codec = SkiaSharp.SKCodec.Create(new MemoryStream(png));
        if (codec == null)
            return null;
        using var bitmap = SkiaSharp.SKBitmap.Decode(codec);
        if (bitmap == null)
            return null;
        using var converted = bitmap.ColorType == SkiaSharp.SKColorType.Bgra8888
            ? null
            : bitmap.Copy(SkiaSharp.SKColorType.Bgra8888);
        var bgra = converted ?? bitmap;
        var width = bgra.Width;
        var height = bgra.Height;
        const int header = 40;
        var stride = width * 4;
        var dib = new byte[header + stride * height];
        BitConverter.TryWriteBytes(dib.AsSpan(0), header);
        BitConverter.TryWriteBytes(dib.AsSpan(4), width);
        BitConverter.TryWriteBytes(dib.AsSpan(8), height);
        BitConverter.TryWriteBytes(dib.AsSpan(12), (short)1);
        BitConverter.TryWriteBytes(dib.AsSpan(14), (short)32);
        for (var y = 0; y < height; y++)
            Marshal.Copy(bgra.GetPixels() + ((height - 1 - y) * bgra.RowBytes), dib, header + y * stride, stride);
        return dib;
    }

    private static nint MacChangeCount()
    {
        var board = Pasteboard();
        return board == IntPtr.Zero ? 0 : ObjC.SendRetNint(board, ObjC.Sel("changeCount"));
    }

    private static void SetMac(byte[] png)
    {
        var board = Pasteboard();
        if (board == IntPtr.Zero)
            return;
        ObjC.SendRetNint(board, ObjC.Sel("clearContents"));
        SetData(board, NsData(png), NsString("public.png"));
    }

    private static byte[]? ReadMac()
    {
        var board = Pasteboard();
        if (board == IntPtr.Zero)
            return null;
        var types = ObjC.Send(board, ObjC.Sel("types"));
        if (types == IntPtr.Zero)
            return null;
        var count = ObjC.SendRetNint(types, ObjC.Sel("count"));
        var pure = false;
        for (nint i = 0; i < count; i++)
        {
            var item = ObjC.SendNint(types, ObjC.Sel("objectAtIndex:"), i);
            var text = NsToString(item);
            if (text is "public.utf8-plain-text" or "public.file-url" or "NSFilenamesPboardType" or "org.nspasteboard.ConcealedType" or "org.nspasteboard.TransientType")
                return null;
            if (text is "public.png" or "public.tiff")
                pure = true;
        }
        if (!pure)
            return null;
        foreach (var kind in new[] { "public.png", "public.tiff" })
        {
            var data = DataForType(board, kind);
            if (data == null)
                continue;
            if (kind == "public.png")
                return data;
            using var bitmap = SkiaSharp.SKBitmap.Decode(data);
            if (bitmap == null)
                return null;
            using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        return null;
    }

    private static void SetData(IntPtr board, IntPtr data, IntPtr type)
    {
        ObjC.Send2(board, ObjC.Sel("setData:forType:"), data, type);
    }

    private static byte[]? DataForType(IntPtr board, string kind)
    {
        var data = ObjC.Send1(board, ObjC.Sel("dataForType:"), NsString(kind));
        if (data == IntPtr.Zero)
            return null;
        var length = ObjC.SendRetNint(data, ObjC.Sel("length"));
        if (length <= 8 || length > ImageFiles.MaxBytes)
            return null;
        var bytes = ObjC.Send(data, ObjC.Sel("bytes"));
        var buffer = new byte[length];
        Marshal.Copy(bytes, buffer, 0, (int)length);
        return buffer;
    }

    private static IntPtr Pasteboard()
        => ObjC.Send(ObjC.Class("NSPasteboard"), ObjC.Sel("generalPasteboard"));

    private static IntPtr NsString(string text)
        => ObjC.SendCString(ObjC.Class("NSString"), ObjC.Sel("stringWithUTF8String:"), text);

    private static IntPtr NsData(byte[] bytes)
    {
        var mem = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, mem, bytes.Length);
        var data = ObjC.SendBytes(ObjC.Class("NSData"), ObjC.Sel("dataWithBytes:length:"), mem, bytes.Length);
        Marshal.FreeHGlobal(mem);
        return data;
    }

    private static string NsToString(IntPtr ns)
    {
        var utf = ObjC.Send(ns, ObjC.Sel("UTF8String"));
        return utf == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(utf) ?? "";
    }

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr mem);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(IntPtr mem);
}
