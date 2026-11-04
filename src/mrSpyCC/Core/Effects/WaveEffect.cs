using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class WaveEffect : EffectBase
{
    public override string Id => "wave";
    public override string DisplayName => "Dalga (Wave)";

    public double AngleDegrees { get; set; } = 90.0;
    public double Speed { get; set; } = 1.5;
    public double Frequency { get; set; } = 0.25;
    public int PaletteIndex { get; set; } = 0; // 0=Rainbow, 1=Cyberpunk, 2=Sunset, 3=Ocean

    private double _time;

    public override void Reset()
    {
        _time = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed * 2.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        double rad = AngleDegrees * Math.PI / 180.0;
        double dirX = Math.Cos(rad);
        double dirY = Math.Sin(rad);

        var palette = PaletteIndex switch
        {
            1 => CyberpunkPalette,
            2 => SunsetPalette,
            3 => OceanPalette,
            _ => RainbowPalette
        };

        foreach (var key in KeyMapTable.AllKeys)
        {
            double proj = key.X * dirX + key.Y * dirY;
            double phase = (_time + proj * Frequency);

            if (PaletteIndex == 0)
            {
                // Smooth continuous HSV spectrum
                double hue = (phase * 60.0) % 360.0;
                buffer.SetKey(key.Offset, ColorRgb.FromHsv(hue));
            }
            else
            {
                // Multi-stop palette interpolation
                double pos = (phase % palette.Length + palette.Length) % palette.Length;
                int idx1 = (int)pos;
                int idx2 = (idx1 + 1) % palette.Length;
                double frac = pos - idx1;

                var color = ColorRgb.Lerp(palette[idx1], palette[idx2], frac);
                buffer.SetKey(key.Offset, color);
            }
        }
    }
}
