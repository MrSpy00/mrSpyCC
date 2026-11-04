using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class ColorCycleEffect : EffectBase
{
    public override string Id => "color_cycle";
    public override string DisplayName => "Renk Döngüsü (Color Cycle)";

    public double Speed { get; set; } = 0.8;
    public int PaletteIndex { get; set; } = 0; // 0=Cyberpunk, 1=Sunset, 2=Ocean, 3=Purple/Cyan

    private double _time;

    private static readonly ColorRgb[] PurpleCyanPalette =
    [
        new(139, 92, 246),  // Neon Violet
        new(0, 240, 255),   // Cyan
        new(236, 72, 153),  // Pink
        new(59, 130, 246)   // Blue
    ];

    public override void Reset()
    {
        _time = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;
    }

    public override void Render(FrameBuffer buffer)
    {
        var palette = PaletteIndex switch
        {
            1 => SunsetPalette,
            2 => OceanPalette,
            3 => PurpleCyanPalette,
            _ => CyberpunkPalette
        };

        double pos = (_time % palette.Length + palette.Length) % palette.Length;
        int idx1 = (int)pos;
        int idx2 = (idx1 + 1) % palette.Length;
        double frac = pos - idx1;

        // Smooth cosine ease between palette colors
        double smoothFrac = 0.5 * (1.0 - Math.Cos(frac * Math.PI));
        var color = ColorRgb.Lerp(palette[idx1], palette[idx2], smoothFrac);

        foreach (var key in KeyMapTable.AllKeys)
        {
            buffer.SetKey(key.Offset, color);
        }
    }
}
