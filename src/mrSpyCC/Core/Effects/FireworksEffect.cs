using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class FireworksEffect : EffectBase
{
    public override string Id => "fireworks";
    public override string DisplayName => "Havai Fişek (Fireworks)";

    public double Speed { get; set; } = 1.0;
    public double SpawnRate { get; set; } = 0.8;
    public bool ReactiveExplosion { get; set; } = true;

    private readonly List<Rocket> _rockets = new();
    private readonly List<Spark> _sparks = new();
    private readonly Random _rnd = new();
    private double _time;
    private double _spawnCooldown;

    private sealed class Rocket
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double TargetY { get; set; }
        public double SpeedY { get; set; }
        public ColorRgb Color { get; set; }
    }

    private sealed class Spark
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Life { get; set; }
        public double MaxLife { get; set; }
        public ColorRgb Color { get; set; }
    }

    public override void Reset()
    {
        _rockets.Clear();
        _sparks.Clear();
        _time = 0.0;
        _spawnCooldown = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _spawnCooldown -= dt;

        // 1. Spawn autonomous rockets
        if (_spawnCooldown <= 0.0)
        {
            _spawnCooldown = (1.2 / Math.Max(0.2, SpawnRate)) + (_rnd.NextDouble() * 0.5);
            _rockets.Add(new Rocket
            {
                X = 2.0 + _rnd.NextDouble() * 18.0,
                Y = 6.0,
                TargetY = 0.5 + _rnd.NextDouble() * 3.0,
                SpeedY = -(3.5 + _rnd.NextDouble() * 2.5) * Speed,
                Color = ColorRgb.FromHsv(_rnd.Next(0, 360))
            });
        }

        // 2. Update rockets
        for (int i = _rockets.Count - 1; i >= 0; i--)
        {
            var r = _rockets[i];
            r.Y += r.SpeedY * dt;

            if (r.Y <= r.TargetY)
            {
                // Explode!
                Explode(r.X, r.Y, r.Color);
                _rockets.RemoveAt(i);
            }
        }

        // 3. Update sparks
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Life -= dt * 1.6;
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt + (1.2 * dt * dt); // gentle gravity
            s.Vx *= 0.92; // air drag
            s.Vy *= 0.92;

            if (s.Life <= 0.0)
            {
                _sparks.RemoveAt(i);
            }
        }
    }

    private void Explode(double x, double y, ColorRgb color)
    {
        int count = 16 + _rnd.Next(0, 10);
        for (int i = 0; i < count; i++)
        {
            double angle = (_rnd.NextDouble() * 2.0 * Math.PI);
            double speed = (1.5 + _rnd.NextDouble() * 3.5) * Speed;
            double life = 0.6 + _rnd.NextDouble() * 0.8;

            ColorRgb sparkColor = _rnd.NextDouble() < 0.25 ? ColorRgb.White : color;

            _sparks.Add(new Spark
            {
                X = x,
                Y = y,
                Vx = Math.Cos(angle) * speed * 2.2, // scale X aspect ratio for keyboard
                Vy = Math.Sin(angle) * speed,
                Life = life,
                MaxLife = life,
                Color = sparkColor
            });
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown || !ReactiveExplosion) return;
        Explode(key.X, key.Y, ColorRgb.FromHsv(_rnd.Next(0, 360)));
    }

    public override void Render(FrameBuffer buffer)
    {
        // Dark trail background
        buffer.Clear();

        // Render rockets
        foreach (var r in _rockets)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                double dx = key.X - r.X;
                double dy = key.Y - r.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1.0)
                {
                    double alpha = 1.0 - dist;
                    buffer.BlendKey(key.Offset, r.Color, alpha);
                }
            }
        }

        // Render sparks
        foreach (var s in _sparks)
        {
            double alpha = Math.Clamp(s.Life / s.MaxLife, 0.0, 1.0);
            ColorRgb col = s.Color.Scale(alpha);

            foreach (var key in KeyMapTable.AllKeys)
            {
                double dx = key.X - s.X;
                double dy = (key.Y - s.Y) * 1.8;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 1.1)
                {
                    double kAlpha = (1.1 - dist) / 1.1 * alpha;
                    buffer.BlendKey(key.Offset, col, kAlpha);
                }
            }
        }
    }
}
