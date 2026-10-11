using System.Collections.Frozen;

namespace DispHub.Core.Hotkeys;

/// <summary>Names for the Windows virtual-key codes that can be part of a hotkey.</summary>
public static class KeyNames
{
    private static readonly FrozenDictionary<int, string> NamesByKey = BuildNames();
    private static readonly FrozenDictionary<string, int> KeysByName =
        NamesByKey.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(int virtualKey) => NamesByKey.ContainsKey(virtualKey);

    public static bool IsFunctionKey(int virtualKey) => virtualKey is >= 0x70 and <= 0x87;

    /// <summary>Ctrl, Alt, Shift and Win on their own are never the main key of a hotkey.</summary>
    public static bool IsModifierKey(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    public static string GetName(int virtualKey) =>
        NamesByKey.TryGetValue(virtualKey, out string? name) ? name : $"0x{virtualKey:X2}";

    public static bool TryGetKey(string name, out int virtualKey) => KeysByName.TryGetValue(name, out virtualKey);

    private static FrozenDictionary<int, string> BuildNames()
    {
        var names = new Dictionary<int, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x13] = "Pause",
            [0x14] = "CapsLock",
            [0x1B] = "Esc",
            [0x20] = "Space",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2C] = "PrintScreen",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x6A] = "NumMultiply",
            [0x6B] = "NumPlus",
            [0x6D] = "NumMinus",
            [0x6E] = "NumDecimal",
            [0x6F] = "NumDivide",
            [0x90] = "NumLock",
            [0x91] = "ScrollLock",
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'",
        };

        for (int digit = 0; digit <= 9; digit++)
        {
            names[0x30 + digit] = digit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            names[0x60 + digit] = $"Num{digit}";
        }

        for (char letter = 'A'; letter <= 'Z'; letter++)
            names[letter] = letter.ToString();

        for (int f = 1; f <= 24; f++)
            names[0x6F + f] = $"F{f}";

        return names.ToFrozenDictionary();
    }
}
