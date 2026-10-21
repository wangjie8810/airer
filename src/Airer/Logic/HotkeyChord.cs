namespace Airer;

public sealed class HotkeyChord
{
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Meta { get; set; }
    public string Key { get; set; } = "T";

    public static HotkeyChord ToggleDefault() => new() { Control = true, Alt = true, Key = "T" };
    public static HotkeyChord RegionDefault() => new() { Control = true, Alt = true, Key = "S" };
    public static HotkeyChord ScreenDefault() => new() { Control = true, Alt = true, Key = "3" };

    public bool IsValid => (Control || Alt || Meta) && Key != null && Normalize(Key) == Key;

    public static HotkeyChord? TryCreate(bool control, bool alt, bool shift, bool meta, string? key)
    {
        var normalized = Normalize(key);
        if (normalized == null || (!control && !alt && !meta))
            return null;
        return new HotkeyChord
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Meta = meta,
            Key = normalized,
        };
    }

    public static string? Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        var trimmed = key.Trim();
        if (trimmed.Equals("space", StringComparison.OrdinalIgnoreCase))
            return "Space";
        if (trimmed.Length >= 2 && (trimmed[0] == 'F' || trimmed[0] == 'f') && int.TryParse(trimmed[1..], out var f) && f is >= 1 and <= 12)
            return "F" + f;
        if (trimmed.Length == 1)
        {
            var c = char.ToUpperInvariant(trimmed[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                return c.ToString();
        }
        return null;
    }

    public string Display()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add(OperatingSystem.IsMacOS() ? "Opt" : "Alt");
        if (Shift) parts.Add("Shift");
        if (Meta) parts.Add(OperatingSystem.IsMacOS() ? "Cmd" : "Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }

    public uint WindowsModifiers()
    {
        uint mods = 0x4000; // MOD_NOREPEAT
        if (Alt) mods |= 0x0001;
        if (Control) mods |= 0x0002;
        if (Shift) mods |= 0x0004;
        if (Meta) mods |= 0x0008;
        return mods;
    }

    public uint? WindowsVirtualKey() => Key switch
    {
        "Space" => 0x20,
        "0" => 0x30,
        "1" => 0x31,
        "2" => 0x32,
        "3" => 0x33,
        "4" => 0x34,
        "5" => 0x35,
        "6" => 0x36,
        "7" => 0x37,
        "8" => 0x38,
        "9" => 0x39,
        "F1" => 0x70,
        "F2" => 0x71,
        "F3" => 0x72,
        "F4" => 0x73,
        "F5" => 0x74,
        "F6" => 0x75,
        "F7" => 0x76,
        "F8" => 0x77,
        "F9" => 0x78,
        "F10" => 0x79,
        "F11" => 0x7A,
        "F12" => 0x7B,
        _ when Key.Length == 1 && Key[0] is >= 'A' and <= 'Z' => Key[0],
        _ => null,
    };

    public uint MacModifiers()
    {
        uint mods = 0;
        if (Meta) mods |= 1u << 8;
        if (Shift) mods |= 1u << 9;
        if (Alt) mods |= 1u << 11;
        if (Control) mods |= 1u << 12;
        return mods;
    }

    public uint? MacKeyCode() => Key switch
    {
        "A" => 0x00,
        "S" => 0x01,
        "D" => 0x02,
        "F" => 0x03,
        "H" => 0x04,
        "G" => 0x05,
        "Z" => 0x06,
        "X" => 0x07,
        "C" => 0x08,
        "V" => 0x09,
        "B" => 0x0B,
        "Q" => 0x0C,
        "W" => 0x0D,
        "E" => 0x0E,
        "R" => 0x0F,
        "Y" => 0x10,
        "T" => 0x11,
        "1" => 0x12,
        "2" => 0x13,
        "3" => 0x14,
        "4" => 0x15,
        "6" => 0x16,
        "5" => 0x17,
        "9" => 0x19,
        "7" => 0x1A,
        "8" => 0x1C,
        "0" => 0x1D,
        "O" => 0x1F,
        "U" => 0x20,
        "I" => 0x22,
        "P" => 0x23,
        "L" => 0x25,
        "J" => 0x26,
        "K" => 0x28,
        "N" => 0x2D,
        "M" => 0x2E,
        "Space" => 0x31,
        "F1" => 0x7A,
        "F2" => 0x78,
        "F3" => 0x63,
        "F4" => 0x76,
        "F5" => 0x60,
        "F6" => 0x61,
        "F7" => 0x62,
        "F8" => 0x64,
        "F9" => 0x65,
        "F10" => 0x6D,
        "F11" => 0x67,
        "F12" => 0x6F,
        _ => null,
    };
}
