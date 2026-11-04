using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace mrSpyCC.Core.KeyMatrix;

public static class KeyMapTable
{
    public const int ScKeyOffset = 333;
    public const int IsoEnterPrimaryOffset = 349;
    public const int IsoEnterSecondaryOffset = 321;

    private static readonly List<KeyEntry> _entries = new()
    {
        // Row 0: Function row & Navigation top (16 main keys: Esc, F1..F12, Sc, PrtSc, Del + 4 Numpad top keys: Home, PgUp, PgDn, End)
        new("Esc", 21, 0, 0, 0.0, 0.0, 0.925, 1.0, "Esc", 0x1B, false, KeyGroup.FunctionRow),
        new("F1", 45, 0, 1, 1.025, 0.0, 0.925, 1.0, "F1", 0x70, false, KeyGroup.FunctionRow),
        new("F2", 69, 0, 2, 1.95, 0.0, 0.925, 1.0, "F2", 0x71, false, KeyGroup.FunctionRow),
        new("F3", 93, 0, 3, 2.875, 0.0, 0.925, 1.0, "F3", 0x72, false, KeyGroup.FunctionRow),
        new("F4", 117, 0, 4, 3.8, 0.0, 0.925, 1.0, "F4", 0x73, false, KeyGroup.FunctionRow),
        new("F5", 141, 0, 5, 4.825, 0.0, 0.925, 1.0, "F5", 0x74, false, KeyGroup.FunctionRow),
        new("F6", 165, 0, 6, 5.75, 0.0, 0.925, 1.0, "F6", 0x75, false, KeyGroup.FunctionRow),
        new("F7", 189, 0, 7, 6.675, 0.0, 0.925, 1.0, "F7", 0x76, false, KeyGroup.FunctionRow),
        new("F8", 213, 0, 8, 7.6, 0.0, 0.925, 1.0, "F8", 0x77, false, KeyGroup.FunctionRow),
        new("F9", 237, 0, 9, 8.625, 0.0, 0.925, 1.0, "F9", 0x78, false, KeyGroup.FunctionRow),
        new("F10", 261, 0, 10, 9.55, 0.0, 0.925, 1.0, "F10", 0x79, false, KeyGroup.FunctionRow),
        new("F11", 285, 0, 11, 10.475, 0.0, 0.925, 1.0, "F11", 0x7A, false, KeyGroup.FunctionRow),
        new("F12", 309, 0, 12, 11.4, 0.0, 0.925, 1.0, "F12", 0x7B, false, KeyGroup.FunctionRow),
        new("Sc", 333, 0, 13, 12.325, 0.0, 0.925, 1.0, "Sc", 0x91, false, KeyGroup.FunctionRow),
        new("PrtSc", 357, 0, 14, 13.25, 0.0, 0.925, 1.0, "PrtSc", 0x2C, false, KeyGroup.FunctionRow),
        new("Del", 381, 0, 15, 14.175, 0.0, 0.925, 1.0, "Del", 0x2E, false, KeyGroup.FunctionRow),
        new("Home", 405, 0, 16, 16.35, 0.0, 1.0, 1.0, "Home", 0x24, false, KeyGroup.FunctionRow),
        new("PgUp", 429, 0, 17, 17.35, 0.0, 1.0, 1.0, "PgUp", 0x21, false, KeyGroup.FunctionRow),
        new("PgDn", 453, 0, 18, 18.35, 0.0, 1.0, 1.0, "PgDn", 0x22, false, KeyGroup.FunctionRow),
        new("End", 477, 0, 19, 19.35, 0.0, 1.0, 1.0, "End", 0x23, false, KeyGroup.FunctionRow),

        // Row 1: Numbers & Backspace & Numpad Top
        new("Grave", 17, 1, 0, 0.0, 1.0, 1.0, 1.0, "\"", 0xC0, false, KeyGroup.Numbers),
        new("1", 41, 1, 1, 1.0, 1.0, 1.0, 1.0, "1", '1', false, KeyGroup.Numbers),
        new("2", 65, 1, 2, 2.0, 1.0, 1.0, 1.0, "2", '2', false, KeyGroup.Numbers),
        new("3", 89, 1, 3, 3.0, 1.0, 1.0, 1.0, "3", '3', false, KeyGroup.Numbers),
        new("4", 113, 1, 4, 4.0, 1.0, 1.0, 1.0, "4", '4', false, KeyGroup.Numbers),
        new("5", 137, 1, 5, 5.0, 1.0, 1.0, 1.0, "5", '5', false, KeyGroup.Numbers),
        new("6", 161, 1, 6, 6.0, 1.0, 1.0, 1.0, "6", '6', false, KeyGroup.Numbers),
        new("7", 185, 1, 7, 7.0, 1.0, 1.0, 1.0, "7", '7', false, KeyGroup.Numbers),
        new("8", 209, 1, 8, 8.0, 1.0, 1.0, 1.0, "8", '8', false, KeyGroup.Numbers),
        new("9", 233, 1, 9, 9.0, 1.0, 1.0, 1.0, "9", '9', false, KeyGroup.Numbers),
        new("0", 257, 1, 10, 10.0, 1.0, 1.0, 1.0, "0", '0', false, KeyGroup.Numbers),
        new("Minus", 281, 1, 11, 11.0, 1.0, 1.0, 1.0, "*", 0xBD, false, KeyGroup.Numbers),
        new("Equals", 305, 1, 12, 12.0, 1.0, 1.0, 1.0, "-", 0xBB, false, KeyGroup.Numbers),
        new("Backspace", 353, 1, 13, 13.0, 1.0, 2.1, 1.0, "Bksp", 0x08, false, KeyGroup.Modifiers),
        new("NumLock", 377, 1, 14, 16.35, 1.0, 1.0, 1.0, "Num", 0x90, false, KeyGroup.Numpad),
        new("NumpadDiv", 401, 1, 15, 17.35, 1.0, 1.0, 1.0, "/", 0x6F, true, KeyGroup.Numpad),
        new("NumpadMul", 425, 1, 16, 18.35, 1.0, 1.0, 1.0, "*", 0x6A, false, KeyGroup.Numpad),
        new("NumpadMinus", 449, 1, 17, 19.35, 1.0, 1.0, 1.0, "-", 0x6D, false, KeyGroup.Numpad),

        // Row 2: Tab, QWERTY, Turkish Ğ, Ü, Enter, Numpad 7-9, +
        new("Tab", 13, 2, 0, 0.0, 2.0, 1.5, 1.0, "Tab", 0x09, false, KeyGroup.Modifiers),
        new("Q", 61, 2, 1, 1.5, 2.0, 1.0, 1.0, "Q", 'Q', false, KeyGroup.Alphanumeric),
        new("W", 85, 2, 2, 2.5, 2.0, 1.0, 1.0, "W", 'W', false, KeyGroup.Wasd),
        new("E", 109, 2, 3, 3.5, 2.0, 1.0, 1.0, "E", 'E', false, KeyGroup.Alphanumeric),
        new("R", 133, 2, 4, 4.5, 2.0, 1.0, 1.0, "R", 'R', false, KeyGroup.Alphanumeric),
        new("T", 157, 2, 5, 5.5, 2.0, 1.0, 1.0, "T", 'T', false, KeyGroup.Alphanumeric),
        new("Y", 181, 2, 6, 6.5, 2.0, 1.0, 1.0, "Y", 'Y', false, KeyGroup.Alphanumeric),
        new("U", 205, 2, 7, 7.5, 2.0, 1.0, 1.0, "U", 'U', false, KeyGroup.Alphanumeric),
        new("I", 229, 2, 8, 8.5, 2.0, 1.0, 1.0, "I", 'I', false, KeyGroup.Alphanumeric),
        new("O", 253, 2, 9, 9.5, 2.0, 1.0, 1.0, "O", 'O', false, KeyGroup.Alphanumeric),
        new("P", 277, 2, 10, 10.5, 2.0, 1.0, 1.0, "P", 'P', false, KeyGroup.Alphanumeric),
        new("BracketL", 301, 2, 11, 11.5, 2.0, 1.0, 1.0, "Ğ", 0xDB, false, KeyGroup.Alphanumeric),
        new("BracketR", 325, 2, 12, 12.5, 2.0, 1.0, 1.0, "Ü", 0xDD, false, KeyGroup.Alphanumeric),
        // ISO Vertical Enter (Clean 1.6-width key covering row 2 and reaching row 3)
        new("Enter", 349, 2, 13, 13.5, 2.0, 1.6, 2.0, "Enter", 0x0D, false, KeyGroup.Modifiers),
        new("Numpad7", 373, 2, 14, 16.35, 2.0, 1.0, 1.0, "7", 0x67, false, KeyGroup.Numpad),
        new("Numpad8", 397, 2, 15, 17.35, 2.0, 1.0, 1.0, "8", 0x68, false, KeyGroup.Numpad),
        new("Numpad9", 421, 2, 16, 18.35, 2.0, 1.0, 1.0, "9", 0x69, false, KeyGroup.Numpad),
        new("NumpadPlus", 445, 2, 17, 19.35, 2.0, 1.0, 2.0, "+", 0x6B, false, KeyGroup.Numpad),

        // Row 3: Caps, ASDF, Turkish Ş, İ, Numpad 4-6
        new("CapsLock", 9, 3, 0, 0.0, 3.0, 1.75, 1.0, "Caps", 0x14, false, KeyGroup.Modifiers),
        new("A", 57, 3, 1, 1.75, 3.0, 1.0, 1.0, "A", 'A', false, KeyGroup.Wasd),
        new("S", 81, 3, 2, 2.75, 3.0, 1.0, 1.0, "S", 'S', false, KeyGroup.Wasd),
        new("D", 105, 3, 3, 3.75, 3.0, 1.0, 1.0, "D", 'D', false, KeyGroup.Wasd),
        new("F", 129, 3, 4, 4.75, 3.0, 1.0, 1.0, "F", 'F', false, KeyGroup.Alphanumeric),
        new("G", 153, 3, 5, 5.75, 3.0, 1.0, 1.0, "G", 'G', false, KeyGroup.Alphanumeric),
        new("H", 177, 3, 6, 6.75, 3.0, 1.0, 1.0, "H", 'H', false, KeyGroup.Alphanumeric),
        new("J", 201, 3, 7, 7.75, 3.0, 1.0, 1.0, "J", 'J', false, KeyGroup.Alphanumeric),
        new("K", 225, 3, 8, 8.75, 3.0, 1.0, 1.0, "K", 'K', false, KeyGroup.Alphanumeric),
        new("L", 249, 3, 9, 9.75, 3.0, 1.0, 1.0, "L", 'L', false, KeyGroup.Alphanumeric),
        new("Semicolon", 273, 3, 10, 10.75, 3.0, 1.0, 1.0, "Ş", 0xBA, false, KeyGroup.Alphanumeric),
        new("Quote", 297, 3, 11, 11.75, 3.0, 1.0, 1.0, "İ", 0xDE, false, KeyGroup.Alphanumeric),
        // Secondary Enter LED in hardware matrix linked cleanly (matches ISO Enter coordinates for spatial uniformity)
        new("Backslash", 321, 3, 12, 13.5, 2.0, 1.6, 2.0, "", 0xDC, false, KeyGroup.Modifiers),
        new("Numpad4", 369, 3, 13, 16.35, 3.0, 1.0, 1.0, "4", 0x64, false, KeyGroup.Numpad),
        new("Numpad5", 393, 3, 14, 17.35, 3.0, 1.0, 1.0, "5", 0x65, false, KeyGroup.Numpad),
        new("Numpad6", 417, 3, 15, 18.35, 3.0, 1.0, 1.0, "6", 0x66, false, KeyGroup.Numpad),

        // Row 4: ShiftL, ISO < > |, ZXCV, Turkish Ö, Ç, ., ShiftR, Numpad 1-3, NumpadEnter
        new("ShiftL", 29, 4, 0, 0.0, 4.0, 1.25, 1.0, "Shift", 0xA0, false, KeyGroup.Modifiers),
        new("ISO_Backslash", 53, 4, 1, 1.25, 4.0, 1.0, 1.0, "<", 0xE2, false, KeyGroup.Alphanumeric),
        new("Z", 77, 4, 2, 2.25, 4.0, 1.0, 1.0, "Z", 'Z', false, KeyGroup.Alphanumeric),
        new("X", 101, 4, 3, 3.25, 4.0, 1.0, 1.0, "X", 'X', false, KeyGroup.Alphanumeric),
        new("C", 125, 4, 4, 4.25, 4.0, 1.0, 1.0, "C", 'C', false, KeyGroup.Alphanumeric),
        new("V", 149, 4, 5, 5.25, 4.0, 1.0, 1.0, "V", 'V', false, KeyGroup.Alphanumeric),
        new("B", 173, 4, 6, 6.25, 4.0, 1.0, 1.0, "B", 'B', false, KeyGroup.Alphanumeric),
        new("N", 197, 4, 7, 7.25, 4.0, 1.0, 1.0, "N", 'N', false, KeyGroup.Alphanumeric),
        new("M", 221, 4, 8, 8.25, 4.0, 1.0, 1.0, "M", 'M', false, KeyGroup.Alphanumeric),
        new("Comma", 245, 4, 9, 9.25, 4.0, 1.0, 1.0, "Ö", 0xBC, false, KeyGroup.Alphanumeric),
        new("Period", 269, 4, 10, 10.25, 4.0, 1.0, 1.0, "Ç", 0xBE, false, KeyGroup.Alphanumeric),
        new("Slash", 293, 4, 11, 11.25, 4.0, 1.0, 1.0, ".", 0xBF, false, KeyGroup.Alphanumeric),
        new("ShiftR", 341, 4, 12, 12.25, 4.0, 1.75, 1.0, "Shift", 0xA1, false, KeyGroup.Modifiers),
        // Arrow Up sits right next to ShiftR in Row 4
        new("ArrowUp", 337, 4, 13, 14.1, 4.0, 1.0, 1.0, "▲", 0x26, true, KeyGroup.Arrows),
        new("Numpad1", 365, 4, 14, 16.35, 4.0, 1.0, 1.0, "1", 0x61, false, KeyGroup.Numpad),
        new("Numpad2", 389, 4, 15, 17.35, 4.0, 1.0, 1.0, "2", 0x62, false, KeyGroup.Numpad),
        new("Numpad3", 413, 4, 16, 18.35, 4.0, 1.0, 1.0, "3", 0x63, false, KeyGroup.Numpad),
        new("NumpadEnter", 437, 4, 17, 19.35, 4.0, 1.0, 2.0, "Ent", 0x0D, true, KeyGroup.Numpad),

        // Row 5: CtrlL, Fn, Win, AltL, Space, AltGr, Copilot, Arrows ◄ ▼ ►, Numpad 0, .
        new("CtrlL", 1, 5, 0, 0.0, 5.0, 1.25, 1.0, "Ctrl", 0xA2, false, KeyGroup.Modifiers),
        new("Fn", 49, 5, 1, 1.25, 5.0, 1.0, 1.0, "Fn", 0, false, KeyGroup.Modifiers),
        new("Windows", 73, 5, 2, 2.25, 5.0, 1.25, 1.0, "Win", 0x5B, false, KeyGroup.Modifiers),
        new("AltL", 97, 5, 3, 3.5, 5.0, 1.25, 1.0, "Alt", 0xA4, false, KeyGroup.Modifiers),
        new("Space", 169, 5, 4, 4.75, 5.0, 5.25, 1.0, "Space", 0x20, false, KeyGroup.Alphanumeric),
        new("AltGr", 241, 5, 5, 10.0, 5.0, 1.25, 1.0, "AltGr", 0xA5, true, KeyGroup.Modifiers),
        new("CopilotKey", 289, 5, 6, 11.25, 5.0, 1.25, 1.0, "Copilot", 0x86, false, KeyGroup.Modifiers),
        
        // Inverted-T Arrows: Left, Down (directly beneath Up), Right
        new("ArrowLeft", 313, 5, 7, 13.0, 5.0, 1.0, 1.0, "◄", 0x25, true, KeyGroup.Arrows),
        new("ArrowDown", 433, 5, 8, 14.1, 5.0, 1.0, 1.0, "▼", 0x28, true, KeyGroup.Arrows),
        new("ArrowRight", 361, 5, 9, 15.2, 5.0, 1.0, 1.0, "►", 0x27, true, KeyGroup.Arrows),

        // Numpad 0 & .
        new("Numpad0", 385, 5, 10, 16.35, 5.0, 2.0, 1.0, "0", 0x60, false, KeyGroup.Numpad),
        new("NumpadDecimal", 409, 5, 11, 18.35, 5.0, 1.0, 1.0, ".", 0x6E, false, KeyGroup.Numpad),
    };

    public static ReadOnlyCollection<KeyEntry> AllKeys { get; } = _entries.AsReadOnly();

    private static readonly Dictionary<int, KeyEntry> _byOffset = new();
    private static readonly Dictionary<(int vk, bool extended), KeyEntry> _byVk = new();
    private static readonly Dictionary<(int row, int col), KeyEntry> _byGrid = new();

    static KeyMapTable()
    {
        foreach (var key in _entries)
        {
            _byOffset[key.Offset] = key;
            _byGrid[(key.Row, key.Col)] = key;
            if (key.VirtualKey != 0)
            {
                _byVk[(key.VirtualKey, key.IsExtended)] = key;
                _byVk.TryAdd((key.VirtualKey, false), key);
            }
        }

        if (_byOffset.TryGetValue(ScKeyOffset, out var scKey))
        {
            _byVk.TryAdd((0xFF, false), scKey);
            _byVk.TryAdd((0xFF, true), scKey);
        }
    }

    public static KeyEntry? FindByOffset(int offset)
    {
        _byOffset.TryGetValue(offset, out var key);
        return key;
    }

    public static KeyEntry? FindByVirtualKey(int vk, bool isExtended)
    {
        if (_byVk.TryGetValue((vk, isExtended), out var key))
            return key;

        if (_byVk.TryGetValue((vk, false), out key))
            return key;

        return null;
    }

    public static KeyEntry? FindByGrid(int row, int col)
    {
        _byGrid.TryGetValue((row, col), out var key);
        return key;
    }

    public static KeyEntry FindNearest(double x, double y)
    {
        KeyEntry best = _entries[0];
        double bestDistSq = double.MaxValue;

        foreach (var key in _entries)
        {
            double cx = key.X + (key.Width * 0.5);
            double cy = key.Y + (key.Height * 0.5);
            double dx = cx - x;
            double dy = cy - y;
            double distSq = (dx * dx) + (dy * dy);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = key;
            }
        }

        return best;
    }
}
