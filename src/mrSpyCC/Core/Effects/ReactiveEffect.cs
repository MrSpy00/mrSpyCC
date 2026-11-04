using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class ReactiveEffect : EffectBase
{
    public override string Id => "reactive";
    public override string DisplayName => "Reaktif (Reactive / Ripple)";

    public enum ReactiveMode
    {
        SingleKey,
        RippleSplash,
        LaserBeam
    }

    public ReactiveMode Mode { get; set; } = ReactiveMode.RippleSplash;
    public double FadeDuration { get; set; } = 1.0;
    public ColorRgb SplashColor { get; set; } = ColorRgb.Cyan;
    public ColorRgb BackgroundColor { get; set; } = ColorRgb.Black;
    public bool RandomColorMode { get; set; } = false;

    private readonly struct ActiveKeypress(int offset, double startTime, ColorRgb color)
    {
        public int Offset { get; } = offset;
        public double StartTime { get; } = startTime;
        public ColorRgb Color { get; } = color;
    }

    private readonly struct ActiveRipple(double originX, double originY, double startTime, ColorRgb color)
    {
        public double OriginX { get; } = originX;
        public double OriginY { get; } = originY;
        public double StartTime { get; } = startTime;
        public ColorRgb Color { get; } = color;
    }

    private readonly struct ActiveBeam(int row, double originX, double startTime, ColorRgb color)
    {
        public int Row { get; } = row;
        public double OriginX { get; } = originX;
        public double StartTime { get; } = startTime;
        public ColorRgb Color { get; } = color;
    }

    private readonly List<ActiveKeypress> _activeKeys = new();
    private readonly List<ActiveRipple> _activeRipples = new();
    private readonly List<ActiveBeam> _activeBeams = new();
    private readonly Random _rnd = new();
    private readonly object _lock = new();
    private double _elapsedTime;

    public override void Reset()
    {
        lock (_lock)
        {
            _activeKeys.Clear();
            _activeRipples.Clear();
            _activeBeams.Clear();
            _elapsedTime = 0.0;
        }
    }

    public override void Update(double dt)
    {
        lock (_lock)
        {
            _elapsedTime += dt;
            _activeKeys.RemoveAll(k => _elapsedTime - k.StartTime > FadeDuration);
            _activeRipples.RemoveAll(r => _elapsedTime - r.StartTime > FadeDuration * 1.5);
            _activeBeams.RemoveAll(b => _elapsedTime - b.StartTime > FadeDuration);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        ColorRgb color = RandomColorMode
            ? ColorRgb.FromHsv(_rnd.Next(0, 360))
            : SplashColor;

        lock (_lock)
        {
            switch (Mode)
            {
                case ReactiveMode.SingleKey:
                    _activeKeys.Add(new ActiveKeypress(key.Offset, _elapsedTime, color));
                    break;

                case ReactiveMode.RippleSplash:
                    _activeKeys.Add(new ActiveKeypress(key.Offset, _elapsedTime, color));
                    _activeRipples.Add(new ActiveRipple(key.X, key.Y, _elapsedTime, color));
                    break;

                case ReactiveMode.LaserBeam:
                    _activeKeys.Add(new ActiveKeypress(key.Offset, _elapsedTime, color));
                    _activeBeams.Add(new ActiveBeam(key.Row, key.X, _elapsedTime, color));
                    break;
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        if (BackgroundColor != ColorRgb.Black)
            buffer.Fill(BackgroundColor);

        lock (_lock)
        {
            // 1. Single Key Fade
            foreach (var k in _activeKeys)
            {
                double age = _elapsedTime - k.StartTime;
                double alpha = Math.Clamp(1.0 - (age / FadeDuration), 0.0, 1.0);
                buffer.BlendKey(k.Offset, k.Color, alpha);
            }

            // 2. Ripple Splash (Expanding circular wave)
            if (Mode == ReactiveMode.RippleSplash)
            {
                const double waveSpeed = 16.0; // units per second across keyboard deck
                const double waveThickness = 1.8;

                foreach (var rip in _activeRipples)
                {
                    double age = _elapsedTime - rip.StartTime;
                    double currentRadius = age * waveSpeed;
                    double fade = Math.Clamp(1.0 - (age / (FadeDuration * 1.5)), 0.0, 1.0);

                    foreach (var key in KeyMapTable.AllKeys)
                    {
                        double dx = key.X - rip.OriginX;
                        double dy = key.Y - rip.OriginY;
                        double dist = Math.Sqrt(dx * dx + dy * dy);

                        double distDiff = Math.Abs(dist - currentRadius);
                        if (distDiff < waveThickness)
                        {
                            double intensity = (1.0 - (distDiff / waveThickness)) * fade;
                            buffer.BlendKey(key.Offset, rip.Color, intensity);
                        }
                    }
                }
            }

            // 3. Laser Beam
            if (Mode == ReactiveMode.LaserBeam)
            {
                const double beamSpeed = 24.0;
                foreach (var beam in _activeBeams)
                {
                    double age = _elapsedTime - beam.StartTime;
                    double currentReach = age * beamSpeed;
                    double fade = Math.Clamp(1.0 - (age / FadeDuration), 0.0, 1.0);

                    foreach (var key in KeyMapTable.AllKeys)
                    {
                        if (key.Row == beam.Row)
                        {
                            double dist = Math.Abs(key.X - beam.OriginX);
                            if (dist <= currentReach)
                            {
                                buffer.BlendKey(key.Offset, beam.Color, fade);
                            }
                        }
                    }
                }
            }
        }
    }
}
