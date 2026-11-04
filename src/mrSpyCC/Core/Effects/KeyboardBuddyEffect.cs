using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class KeyboardBuddyEffect : EffectBase
{
    public override string Id => "keyboard_buddy";
    public override string DisplayName => "Siber Yoldaş (Keyboard Buddy)";

    private sealed class CheerWave
    {
        public double X;
        public double Y;
        public double Radius;
        public double MaxRadius = 4.5;
        public double Life = 0.6;
        public ColorRgb Color = new(0, 240, 255);
    }

    private sealed class TrailPoint
    {
        public double X;
        public double Y;
        public double Life = 0.45;
        public ColorRgb Color;
    }

    private double _x = 6.0;
    private double _y = 3.0;
    private double _targetX = 6.0;
    private double _targetY = 3.0;
    private bool _hasTarget = false;
    private double _skateDirection = 1.0;
    private double _idleTimer = 0;
    private double _time = 0;
    private double _wpm = 0;

    private readonly List<CheerWave> _waves = new();
    private readonly List<TrailPoint> _trails = new();
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _x = 6.0;
        _y = 3.0;
        _targetX = 6.0;
        _targetY = 3.0;
        _hasTarget = false;
        _idleTimer = 0;
        _time = 0;
        _wpm = 0;
        _waves.Clear();
        _trails.Clear();
    }

    public override void Update(double dt)
    {
        _time += dt;
        _idleTimer += dt;
        _wpm = Math.Max(0.0, _wpm - (dt * 1.5));

        // Dalgalar
        for (int i = _waves.Count - 1; i >= 0; i--)
        {
            var w = _waves[i];
            w.Radius += dt * 8.5;
            w.Life -= dt;
            if (w.Life <= 0 || w.Radius > w.MaxRadius)
                _waves.RemoveAt(i);
        }

        // İtici Jet İzi (Trail)
        for (int i = _trails.Count - 1; i >= 0; i--)
        {
            var t = _trails[i];
            t.Life -= dt;
            if (t.Life <= 0)
                _trails.RemoveAt(i);
        }

        if (_hasTarget)
        {
            // Merdiven ve platform zıplayışı ile hedefe koş
            double speed = (12.0 + Math.Min(12.0, _wpm * 2.0)) * dt;
            double dx = _targetX - _x;
            double dy = _targetY - _y;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));

            // Hareket ederken arkasında gökkuşağı renginde jet dumanı bırak
            if (dist > 0.3)
            {
                _x += (dx / dist) * Math.Min(dist, speed);
                _y += (dy / dist) * Math.Min(dist, speed);

                double hue = (_time * 300.0) % 360.0;
                _trails.Add(new TrailPoint
                {
                    X = _x - (dx / dist * 0.5),
                    Y = _y - (dy / dist * 0.5),
                    Color = ColorRgb.FromHsv(hue, 1.0, 1.0)
                });
            }
            else
            {
                // Hedefe ulaşıldı! Kutlama dalgası patlat
                _hasTarget = false;
                _waves.Add(new CheerWave
                {
                    X = _x,
                    Y = _y,
                    Color = _wpm > 6.0 ? new(255, 215, 0) : new(0, 240, 255)
                });
            }
        }
        else
        {
            // Boşta kalınca: Alt barda (Spacebar) kaykay kay veya dikey zıpla
            if (_idleTimer > 3.0)
            {
                _y += (5.0 - _y) * Math.Min(1.0, dt * 3.0);
                _x += _skateDirection * dt * 4.5;
                if (_x > 15.0) _skateDirection = -1.0;
                else if (_x < 3.0) _skateDirection = 1.0;
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu metalik siber zemin
        buffer.Fill(5, 5, 12);

        // 1. Kutlama Dalgaları
        foreach (var w in _waves)
        {
            double progress = w.Radius / w.MaxRadius;
            byte alpha = (byte)((1.0 - progress) * 220);

            foreach (var key in KeyMapTable.AllKeys)
            {
                double kx = key.X + (key.Width * 0.5);
                double ky = key.Y + (key.Height * 0.5);
                double d = Math.Sqrt(Math.Pow(kx - w.X, 2) + Math.Pow(ky - w.Y, 2));

                if (Math.Abs(d - w.Radius) < 0.8)
                {
                    buffer.BlendPixel(key.Offset, w.Color.R, w.Color.G, w.Color.B, alpha);
                }
            }
        }

        // 2. Gökkuşağı Jet İzi
        foreach (var t in _trails)
        {
            var key = KeyMapTable.FindNearest(t.X, t.Y);
            if (key != null)
            {
                byte a = (byte)(Math.Clamp(t.Life / 0.45, 0.0, 1.0) * 180);
                buffer.BlendPixel(key.Offset, t.Color.R, t.Color.G, t.Color.B, a);
            }
        }

        // 3. Yoldaş Robot (Kafa vizörü ve Şasi)
        var headKey = KeyMapTable.FindNearest(_x, _y - 0.4);
        var bodyKey = KeyMapTable.FindNearest(_x, _y);

        if (headKey != null)
        {
            // Vizör: Neon Camgöbeği Gözler
            buffer.SetColor(headKey.Offset, 0, 255, 240);
        }

        if (bodyKey != null && bodyKey.Offset != headKey?.Offset)
        {
            // Şasi: Enerjik Altın/Turuncu
            buffer.SetColor(bodyKey.Offset, 255, 140, 0);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _idleTimer = 0;
            _targetX = key.X + (key.Width * 0.5);
            _targetY = key.Y + (key.Height * 0.5);
            _hasTarget = true;
            _wpm += 1.0;
        }
    }
}
