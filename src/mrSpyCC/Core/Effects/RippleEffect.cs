using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class RippleEffect : EffectBase
{
    public override string Id => "ripple";
    public override string DisplayName => "Dairesel Dalga (Ripple)";

    public double Speed { get; set; } = 8.0;
    public double Wavelength { get; set; } = 3.5;
    public ColorRgb PrimaryColor { get; set; } = new(139, 92, 246); // Purple
    public ColorRgb AccentColor { get; set; } = new(0, 240, 255);   // Cyan

    private double _time;

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
        const double centerX = 10.0;
        const double centerY = 3.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dx = key.X - centerX;
            double dy = (key.Y - centerY) * 1.6;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Sine wave propagating outwards
            double wave = Math.Sin((dist - _time) / Wavelength * Math.PI * 2.0);
            double intensity = 0.5 * (wave + 1.0); // 0.0 to 1.0

            var color = ColorRgb.Lerp(PrimaryColor, AccentColor, intensity);
            buffer.SetKey(key.Offset, color.Scale(0.2 + intensity * 0.8));
        }
    }
}
