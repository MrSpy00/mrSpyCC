using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class JellybeanEffect : EffectBase
{
    public override string Id => "jellybean";
    public override string DisplayName => "Jellybean Damlaları (Jellybean Drops)";

    public double Speed { get; set; } = 3.5;
    public double Density { get; set; } = 0.4;

    private readonly struct Drop(int col, double y, double speed, ColorRgb color)
    {
        public int Col { get; } = col;
        public double Y { get; } = y;
        public double Speed { get; } = speed;
        public ColorRgb Color { get; } = color;
    }

    private readonly List<Drop> _drops = new();
    private readonly Random _rnd = new();

    private static readonly ColorRgb[] JellybeanPalette =
    [
        new(255, 105, 180), // Hot Pink
        new(138, 43, 226),  // Blue Violet
        new(0, 191, 255),   // Deep Sky Blue
        new(0, 250, 154),   // Medium Spring Green
        new(255, 215, 0),   // Gold
        new(255, 69, 0),    // Red Orange
        new(147, 112, 219)  // Medium Purple
    ];

    public override void Reset()
    {
        _drops.Clear();
    }

    public override void Update(double dt)
    {
        // 1. Spawn new jellybean drops
        if (_rnd.NextDouble() < Density)
        {
            int col = _rnd.Next(0, 23);
            double spd = Speed * (0.8 + _rnd.NextDouble() * 0.5);
            var color = JellybeanPalette[_rnd.Next(0, JellybeanPalette.Length)];
            _drops.Add(new Drop(col, -0.5, spd, color));
        }

        // 2. Advance drops
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            double ny = d.Y + d.Speed * dt;
            if (ny > 7.0)
            {
                _drops.RemoveAt(i);
            }
            else
            {
                _drops[i] = new Drop(d.Col, ny, d.Speed, d.Color);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        buffer.Clear();

        foreach (var drop in _drops)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                int kCol = (int)key.X;
                if (kCol == drop.Col)
                {
                    double dist = Math.Abs(key.Y - drop.Y);
                    if (dist < 1.2)
                    {
                        double alpha = 1.0 - (dist / 1.2);
                        buffer.BlendKey(key.Offset, drop.Color, alpha);
                    }
                }
            }
        }
    }
}
