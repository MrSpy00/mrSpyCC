using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class MarqueeEffect : EffectBase
{
    public override string Id => "marquee";
    public override string DisplayName => "Kayan Işık (Marquee)";

    public double Speed { get; set; } = 14.0; // keys per second
    public double RibbonWidth { get; set; } = 4.0;
    public ColorRgb RibbonColor { get; set; } = new(168, 85, 247); // Neon Purple
    public ColorRgb BackgroundColor { get; set; } = new(10, 10, 20);

    private double _position;

    public override void Reset()
    {
        _position = 0.0;
    }

    public override void Update(double dt)
    {
        _position += dt * Speed;
        if (_position > 24.0) _position = -4.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        buffer.Fill(BackgroundColor);

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dist = Math.Abs(key.X - _position);
            if (dist < RibbonWidth)
            {
                double intensity = 1.0 - (dist / RibbonWidth);
                buffer.BlendKey(key.Offset, RibbonColor, intensity);
            }
        }
    }
}
