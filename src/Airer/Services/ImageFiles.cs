using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Airer;

public static class ImageFiles
{
    public const int MaxBytes = 64 * 1024 * 1024;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff",
    };

    public static bool IsImage(string path)
        => Extensions.Contains(Path.GetExtension(path)) && !Path.GetFileName(path).StartsWith('.');

    public static Bitmap? Thumbnail(string path, int maxEdge)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is 0 or > MaxBytes)
                return null;
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);
            if (codec == null)
                return null;
            var src = codec.Info;
            if ((long)src.Width * src.Height > 80_000_000)
                return null;
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap == null)
                return null;
            var edge = Math.Max(bitmap.Width, bitmap.Height);
            var scale = edge > maxEdge ? maxEdge / (float)edge : 1f;
            var w = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
            var h = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
            using var resized = bitmap.Width == w && bitmap.Height == h
                ? bitmap.Copy()
                : bitmap.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            if (resized == null)
                return null;
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return new Bitmap(new MemoryStream(data.ToArray()));
        }
        catch (Exception ex)
        {
            Log.Error($"Thumbnail failed for {path}: {ex.Message}");
            return null;
        }
    }

    public static byte[]? PngBytes(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var raw = File.ReadAllBytes(path);
                return raw.Length <= MaxBytes ? raw : null;
            }
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
                return null;
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            Log.Error($"PNG encode failed for {path}: {ex.Message}");
            return null;
        }
    }

    public static byte[] EncodeBgra(byte[] bgra, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bgra, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            using var image = SKImage.FromPixels(info, handle.AddrOfPinnedObject(), width * 4);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    public static string WriteSample(string folder, string name, SKColor color, int width, int height, string label)
    {
        var path = StoragePaths.UniquePath(folder, name);
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        using var font = new SKFont(SKTypeface.Default, Math.Min(width, height) * 0.28f);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(label, 24, height * 0.62f, font, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }
}
