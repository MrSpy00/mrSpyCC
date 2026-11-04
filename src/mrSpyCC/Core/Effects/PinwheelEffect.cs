using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class PinwheelEffect : EffectBase
{
    public override string Id => "pinwheel";
    public override string DisplayName => "Sarmal Çark (Pinwheel)";

    public double Speed { get; set; } = 1.2;
    public int Arms { get; set; } = 4;
    public double SpiralTightness { get; set; } = 0.35;

    private double _rotation;

    public override void Reset()
    {
        _rotation = 0.0;
    }

    public override void Update(double dt)
    {
        _rotation += dt * Speed * 180.0;
        _rotation = (_rotation % 360.0 + 360.0) % 360.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        const double centerX = 10.0;
        const double centerY = 3.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dx = key.X - centerX;
            double dy = (key.Y - centerY) * 1.7;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            double spiralAngle = (angle + dist * SpiralTightness * 30.0 + _rotation) % 360.0;
            double hue = (spiralAngle * Arms) % 360.0;

            buffer.SetKey(key.Offset, ColorRgb.FromHsv(hue));
        }
    }
}
