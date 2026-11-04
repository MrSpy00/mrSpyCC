using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class StaticPerKeyEffect : EffectBase
{
    public override string Id => "static_per_key";
    public override string DisplayName => "Sabit / Per-Key (Custom)";

    private readonly Dictionary<int, ColorRgb> _keyColors = new();
    private readonly object _lock = new();

    public StaticPerKeyEffect()
    {
        // Default clean aesthetic: Neon Cyan WASD & Arrows, Obsidian Indigo for alphanumeric
        SetGroupColor(KeyGroup.All, new ColorRgb(15, 25, 45));
        SetGroupColor(KeyGroup.Wasd, ColorRgb.Cyan);
        SetGroupColor(KeyGroup.Arrows, ColorRgb.Cyan);
        SetGroupColor(KeyGroup.FunctionRow, ColorRgb.Magenta);
        SetGroupColor(KeyGroup.Numpad, ColorRgb.Yellow);
    }

    public void SetKeyColor(int offset, ColorRgb color)
    {
        lock (_lock)
        {
            _keyColors[offset] = color;
            if (offset == KeyMapTable.IsoEnterPrimaryOffset)
            {
                _keyColors[KeyMapTable.IsoEnterSecondaryOffset] = color;
            }
        }
    }

    public ColorRgb GetKeyColor(int offset)
    {
        lock (_lock)
        {
            return _keyColors.GetValueOrDefault(offset, ColorRgb.Black);
        }
    }

    public void SetGroupColor(KeyGroup group, ColorRgb color)
    {
        lock (_lock)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                if (group == KeyGroup.All || key.Group == group)
                {
                    _keyColors[key.Offset] = color;
                }
            }
        }
    }

    public void ClearAll(ColorRgb defaultColor)
    {
        lock (_lock)
        {
            _keyColors.Clear();
            foreach (var key in KeyMapTable.AllKeys)
            {
                _keyColors[key.Offset] = defaultColor;
            }
        }
    }

    public Dictionary<int, ColorRgb> ExportMap()
    {
        lock (_lock)
        {
            return new Dictionary<int, ColorRgb>(_keyColors);
        }
    }

    public Dictionary<int, string> ExportHexMap()
    {
        lock (_lock)
        {
            var hexMap = new Dictionary<int, string>(_keyColors.Count);
            foreach (var kvp in _keyColors)
            {
                hexMap[kvp.Key] = kvp.Value.ToHex();
            }
            return hexMap;
        }
    }

    public void ImportMap(Dictionary<int, ColorRgb> map)
    {
        lock (_lock)
        {
            _keyColors.Clear();
            foreach (var kvp in map)
            {
                _keyColors[kvp.Key] = kvp.Value;
            }
            EnsureSpecialKeysSynchronized();
        }
    }

    public void ImportHexMap(Dictionary<int, string> hexMap)
    {
        lock (_lock)
        {
            _keyColors.Clear();
            foreach (var kvp in hexMap)
            {
                _keyColors[kvp.Key] = ColorRgb.FromHex(kvp.Value);
            }
            EnsureSpecialKeysSynchronized();
        }
    }

    private void EnsureSpecialKeysSynchronized()
    {
        if (_keyColors.TryGetValue(KeyMapTable.IsoEnterPrimaryOffset, out var isoColor))
        {
            _keyColors[KeyMapTable.IsoEnterSecondaryOffset] = isoColor;
        }

        // Eski kayıtlı profillerde Sc (Offset 333) yoksa komşu F12 (309) veya PrtSc (357) rengini devral
        if (!_keyColors.ContainsKey(KeyMapTable.ScKeyOffset))
        {
            if (_keyColors.TryGetValue(309, out var f12Color) && f12Color != ColorRgb.Black)
            {
                _keyColors[KeyMapTable.ScKeyOffset] = f12Color;
            }
            else if (_keyColors.TryGetValue(357, out var prtScColor) && prtScColor != ColorRgb.Black)
            {
                _keyColors[KeyMapTable.ScKeyOffset] = prtScColor;
            }
            else
            {
                _keyColors[KeyMapTable.ScKeyOffset] = ColorRgb.Magenta;
            }
        }
    }

    public override void Update(double dt) { }

    public override void Render(FrameBuffer buffer)
    {
        lock (_lock)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                if (_keyColors.TryGetValue(key.Offset, out var color))
                {
                    buffer.SetKey(key.Offset, color);
                }
            }
        }
    }
}
