using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class RainbowWaveEffect : EffectBase
{
    public override string Id => "rainbow_wave";
    public override string DisplayName => "Gökkuşağı Dalgası (Rainbow Wave)";

    public double Speed { get; set; } = 2.0;
    public double AngleDegrees { get; set; } = 90.0; // Left to right
    public double Spread { get; set; } = 18.0;

    private double _phase;

    public override void Reset()
    {
        _phase = 0.0;
    }

    public override void Update(double dt)
    {
        _phase += dt * Speed * 100.0;
        _phase = (_phase % 360.0 + 360.0) % 360.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        double rad = AngleDegrees * Math.PI / 180.0;
        double dirX = Math.Cos(rad);
        double dirY = Math.Sin(rad);

        foreach (var key in KeyMapTable.AllKeys)
        {
            double proj = key.X * dirX + key.Y * dirY;
            double hue = (_phase + proj * Spread) % 360.0;
            buffer.SetKey(key.Offset, ColorRgb.FromHsv(hue));
        }
    }
}
