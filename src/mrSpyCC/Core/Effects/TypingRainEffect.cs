using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class TypingRainEffect : EffectBase
{
    public override string Id => "typing_rain";
    public override string DisplayName => "Yazı Yağmuru (Typing Rain)";

    private sealed class RainDrop
    {
        public double X;
        public double Y;
        public double Vy;
        public ColorRgb Color;
    }

    private readonly List<RainDrop> _drops = new();
    private readonly double[] _puddleLevels = new double[512];
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _drops.Clear();
        Array.Clear(_puddleLevels, 0, _puddleLevels.Length);
    }

    public override void Update(double dt)
    {
        // Evaporate puddles
        for (int i = 0; i < _puddleLevels.Length; i++)
        {
            if (_puddleLevels[i] > 0)
                _puddleLevels[i] = Math.Max(0.0, _puddleLevels[i] - (dt * 0.4));
        }

        // Update Falling Drops
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            d.Vy += 22.0 * dt; // Gravity
            d.Y += d.Vy * dt;

            // Check if reached bottom row (Row 5)
            if (d.Y >= 5.0)
            {
                var bottomKey = KeyMapTable.FindNearest(d.X, 5.0);
                if (bottomKey != null)
                {
                    _puddleLevels[bottomKey.Offset] = Math.Min(1.0, _puddleLevels[bottomKey.Offset] + 0.5);
                }
                _drops.RemoveAt(i);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Deep midnight mist background
        buffer.Fill(4, 5, 12);

        // 1. Render Puddles on Bottom Keys
        foreach (var key in KeyMapTable.AllKeys)
        {
            if (key.Row == 5)
            {
                double level = _puddleLevels[key.Offset];
                if (level > 0.05)
                {
                    byte b = (byte)(255 * level);
                    byte g = (byte)(160 * level);
                    byte r = (byte)(20 * level);
                    buffer.BlendPixel(key.Offset, r, g, b, (byte)(level * 220));
                }
            }
        }

        // 2. Render Falling Raindrops
        foreach (var drop in _drops)
        {
            var key = KeyMapTable.FindNearest(drop.X, drop.Y);
            if (key != null)
            {
                buffer.SetColor(key.Offset, drop.Color.R, drop.Color.G, drop.Color.B);
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        // Spawn a raindrop at the typed key position
        if (_drops.Count < 25)
        {
            ColorRgb col = OceanPalette[_rng.Next(OceanPalette.Length)];
            _drops.Add(new RainDrop
            {
                X = key.X + (key.Width * 0.5),
                Y = key.Y + (key.Height * 0.5),
                Vy = 1.5,
                Color = col
            });
        }
    }
}
