using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class WheelEffect : EffectBase
{
    public override string Id => "wheel";
    public override string DisplayName => "Çark (Wheel)";

    public double Speed { get; set; } = 1.0;
    public bool Reverse { get; set; } = false;
    public int Arms { get; set; } = 2; // Number of spiral arms

    private double _angle;

    public override void Reset()
    {
        _angle = 0.0;
    }

    public override void Update(double dt)
    {
        double dir = Reverse ? -1.0 : 1.0;
        _angle += dt * Speed * 180.0 * dir;
        _angle = (_angle % 360.0 + 360.0) % 360.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        const double centerX = 10.0;
        const double centerY = 3.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dx = key.X - centerX;
            double dy = (key.Y - centerY) * 1.8; // Aspect ratio adjustment
            double rad = Math.Atan2(dy, dx);
            double deg = (rad * 180.0 / Math.PI + 360.0) % 360.0;

            double hue = (deg * Arms + _angle) % 360.0;
            buffer.SetKey(key.Offset, ColorRgb.FromHsv(hue));
        }
    }
}
