using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class ScanEffect : EffectBase
{
    public override string Id => "scan";
    public override string DisplayName => "Tarayıcı (Scanner / Cylon)";

    public double Speed { get; set; } = 1.6;
    public double BeamWidth { get; set; } = 1.4;
    public ColorRgb BeamColor { get; set; } = new(255, 10, 40); // Classic Cylon Crimson
    public ColorRgb BackgroundColor { get; set; } = ColorRgb.Black;
    public bool RainbowMode { get; set; } = false;

    private double _pos = 0.0;
    private double _direction = 1.0;
    private double _time = 0.0;

    public override void Reset()
    {
        _pos = 0.0;
        _direction = 1.0;
        _time = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        double minX = 0.0;
        double maxX = 22.0;

        _pos += _direction * Speed * 12.0 * dt;

        if (_pos >= maxX)
        {
            _pos = maxX;
            _direction = -1.0;
        }
        else if (_pos <= minX)
        {
            _pos = minX;
            _direction = 1.0;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        ColorRgb primary = RainbowMode
            ? ColorRgb.FromHsv((_time * 60.0) % 360.0)
            : BeamColor;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dist = Math.Abs(key.X - _pos);

            if (dist <= BeamWidth)
            {
                // Core beam
                double intensity = 1.0 - (dist / BeamWidth);
                var col = ColorRgb.Lerp(primary, ColorRgb.White, intensity * 0.6);
                buffer.SetKey(key.Offset, col);
            }
            else if (dist <= BeamWidth * 2.8)
            {
                // Trailing phosphorescent glow
                double trail = 1.0 - ((dist - BeamWidth) / (BeamWidth * 1.8));
                var col = primary.Scale(trail * 0.6);
                buffer.SetKey(key.Offset, ColorRgb.Lerp(BackgroundColor, col, trail));
            }
            else
            {
                buffer.SetKey(key.Offset, BackgroundColor);
            }
        }
    }
}
