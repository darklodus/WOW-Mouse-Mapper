namespace WowMouseMapper;

public record DetectedInput(string Signal, string Detail, int MouseButton = 0);

public static class InputDecoder
{
    public static string Chord(string key, bool ctrl, bool shift, bool alt) =>
        (ctrl ? "CTRL + " : "") + (shift ? "SHIFT + " : "") + (alt ? "ALT + " : "") + key;

    // RAWMOUSE button flags, as defined by the Windows SDK. Movement and releases are ignored.
    public static IEnumerable<DetectedInput> Mouse(ushort flags, short wheel, bool ctrl, bool shift, bool alt)
    {
        var names = new[] { "Left button", "Right button", "Wheel click", "Side button / X1", "Side button / X2" };
        for (int i = 0; i < 5; i++)
            if ((flags & (1 << (i * 2))) != 0)
                yield return new(Chord($"BUTTON{i + 1}", ctrl, shift, alt), names[i], i + 1);
        if ((flags & 0x400) != 0 && wheel != 0)
            yield return new(Chord(wheel > 0 ? "MOUSEWHEELUP" : "MOUSEWHEELDOWN", ctrl, shift, alt), $"Vertical wheel · delta {wheel}", 3);
        if ((flags & 0x800) != 0 && wheel != 0)
            yield return new(Chord(wheel > 0 ? "WHEELRIGHT" : "WHEELLEFT", ctrl, shift, alt), $"Horizontal wheel · delta {wheel}; check support in WoW", 3);
    }

    public static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5 or 0x5B or 0x5C;

    public static string KeyName(int key, bool extended, string fallback)
    {
        if (key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A) return ((char)key).ToString();
        if (key is >= 0x70 and <= 0x87) return $"F{key - 0x6F}";
        if (key is >= 0x60 and <= 0x69) return $"NUMPAD{key - 0x60}";
        return key switch
        {
            0x0D => extended ? "NUMPAD ENTER" : "ENTER",
            0x20 => "SPACE", 0x09 => "TAB", 0x08 => "BACKSPACE",
            0x6A => "NUMPAD *", 0x6B => "NUMPAD +", 0x6D => "NUMPAD -",
            0x6E => "NUMPAD .", 0x6F => "NUMPAD /",
            _ => fallback.ToUpperInvariant()
        };
    }
}
