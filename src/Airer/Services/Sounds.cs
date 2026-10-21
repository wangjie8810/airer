namespace Airer;

public static class Sounds
{
    private static readonly string Folder = Path.Combine(StoragePaths.AppFolder, "sounds");
    private static string? _tink;
    private static string? _pop;

    public static void Tink() => Play(ref _tink, 880, 70, 0.08);

    public static void Pop() => Play(ref _pop, 520, 50, 0.06);

    public static void Nope()
    {
        if (OperatingSystem.IsWindows())
            System.Media.SystemSounds.Beep.Play();
    }

    private static void Play(ref string? cached, double hz, int ms, double gain)
    {
        try
        {
            cached ??= WriteWav(hz, ms, gain);
            if (OperatingSystem.IsWindows())
            {
                using var player = new System.Media.SoundPlayer(cached);
                player.Play();
                return;
            }
            if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "afplay",
                    ArgumentList = { cached },
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error("Sound failed: " + ex.Message);
        }
    }

    private static string WriteWav(double hz, int ms, double gain)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"{hz:0}-{ms}.wav");
        if (File.Exists(path))
            return path;
        const int rate = 22050;
        var samples = rate * ms / 1000;
        var data = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var env = Math.Exp(-4.0 * i / samples);
            var sample = Math.Sin(2 * Math.PI * hz * i / rate) * env * gain;
            var value = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)value;
            data[i * 2 + 1] = (byte)(value >> 8);
        }
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return path;
    }
}
