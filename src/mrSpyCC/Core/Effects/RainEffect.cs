using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class RainEffect : EffectBase
{
    public override string Id => "rain";
    public override string DisplayName => "Yağmur (Rain)";

    public double Speed { get; set; } = 6.0; // Rows per second
    public double Density { get; set; } = 0.4;
    public double WindAngle { get; set; } = 0.0; // Tilt angle

    private readonly struct Drop(double x, double y, double speed, ColorRgb color)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
        public double Speed { get; } = speed;
        public ColorRgb Color { get; } = color;
    }

    private readonly List<Drop> _drops = new();
    private readonly Random _rnd = new();

    public override void Reset()
    {
        _drops.Clear();
    }

    public override void Update(double dt)
    {
        // 1. Spawn new drops
        if (_rnd.NextDouble() < Density)
        {
            double x = _rnd.NextDouble() * 21.0;
            double spd = Speed * (0.8 + _rnd.NextDouble() * 0.4);
            var col = RainbowPalette[_rnd.Next(0, RainbowPalette.Length)];
            _drops.Add(new Drop(x, -0.5, spd, col));
        }

        // 2. Move drops
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            double ny = d.Y + d.Speed * dt;
            double nx = d.X + Math.Tan(WindAngle * Math.PI / 180.0) * d.Speed * dt;

            if (ny > 6.5)
            {
                _drops.RemoveAt(i);
            }
            else
            {
                _drops[i] = new Drop(nx, ny, d.Speed, d.Color);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        foreach (var drop in _drops)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                double dx = Math.Abs(key.X - drop.X);
                double dy = key.Y - drop.Y; // Distance behind the drop head

                if (dx < 0.8 && dy >= -0.3 && dy < 2.0)
                {
                    double trail = 1.0 - (dy / 2.0);
                    buffer.BlendKey(key.Offset, drop.Color, trail);
                }
            }
        }
    }
}
