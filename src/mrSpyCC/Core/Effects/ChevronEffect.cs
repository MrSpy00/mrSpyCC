using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class ChevronEffect : EffectBase
{
    public override string Id => "chevron";
    public override string DisplayName => "Çavuş Dalgası (Chevron)";

    public double Speed { get; set; } = 4.0;
    public double Wavelength { get; set; } = 6.0;
    public ColorRgb FrontColor { get; set; } = new(168, 85, 247); // Violet
    public ColorRgb BackColor { get; set; } = new(0, 240, 255);   // Cyan

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

        foreach (var key in KeyMapTable.AllKeys)
        {
            // V-shape geometry: distance from center X + Y offset
            double vDist = Math.Abs(key.X - centerX) + (key.Y * 1.5);
            double phase = (vDist - _time) / Wavelength;
            double wave = 0.5 * (Math.Sin(phase * Math.PI * 2.0) + 1.0);

            var color = ColorRgb.Lerp(BackColor, FrontColor, wave);
            buffer.SetKey(key.Offset, color);
        }
    }
}
