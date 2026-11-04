using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class AuroraEffect : EffectBase
{
    public override string Id => "aurora";
    public override string DisplayName => "Kuzey Işıkları (Aurora Borealis)";

    public double Speed { get; set; } = 1.2;
    public double WaveScale { get; set; } = 0.28;
    public double Intensity { get; set; } = 1.0;
    public bool ReactiveShimmer { get; set; } = true;

    private double _time;
    private readonly List<AuroraRipple> _ripples = new();
    private readonly object _lock = new();

    private readonly struct AuroraRipple(double x, double y, double startTime)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
        public double StartTime { get; } = startTime;
    }

    public override void Reset()
    {
        _time = 0.0;
        lock (_lock)
        {
            _ripples.Clear();
        }
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;

        lock (_lock)
        {
            _ripples.RemoveAll(r => _time - r.StartTime > 2.0);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown || !ReactiveShimmer) return;

        lock (_lock)
        {
            if (_ripples.Count > 10)
                _ripples.RemoveAt(0);

            _ripples.Add(new AuroraRipple(key.X, key.Y, _time));
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Aurora ribbons: harmonic sine/cosine overlapping wave fields
        double t1 = _time * 0.7;
        double t2 = _time * 1.1;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double nx = key.X * WaveScale;
            double ny = key.Y * WaveScale;

            // Fluid undulating field
            double wave1 = Math.Sin(nx * 1.4 + t1 + Math.Cos(ny * 2.0 + t2));
            double wave2 = Math.Cos(ny * 2.5 - t2 + Math.Sin(nx * 1.8 - t1));
            double blend = (wave1 + wave2) * 0.5; // -1.0 to 1.0
            double norm = (blend + 1.0) * 0.5;   // 0.0 to 1.0

            // Base northern light colors: Deep Green (0, 255, 120), Cyan (0, 220, 255), Violet (140, 40, 255)
            ColorRgb c1 = new(10, 220, 100);
            ColorRgb c2 = new(0, 190, 255);
            ColorRgb c3 = new(170, 40, 255);

            ColorRgb baseColor;
            if (norm < 0.5)
            {
                baseColor = ColorRgb.Lerp(c1, c2, norm * 2.0);
            }
            else
            {
                baseColor = ColorRgb.Lerp(c2, c3, (norm - 0.5) * 2.0);
            }

            // Curtain height modulation
            double heightFade = Math.Clamp(1.0 - (key.Y / 7.0) * 0.35, 0.2, 1.0);
            baseColor = baseColor.Scale(heightFade * Intensity);

            // Add reactive shimmer if ripples active
            lock (_lock)
            {
                foreach (var rip in _ripples)
                {
                    double dx = key.X - rip.X;
                    double dy = (key.Y - rip.Y) * 1.8;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double age = _time - rip.StartTime;
                    double ring = age * 4.0;
                    double diff = Math.Abs(dist - ring);

                    if (diff < 1.4)
                    {
                        double shimmer = (1.4 - diff) / 1.4 * Math.Max(0.0, 1.0 - age / 2.0);
                        baseColor = ColorRgb.Lerp(baseColor, ColorRgb.White, shimmer * 0.7);
                    }
                }
            }

            buffer.SetKey(key.Offset, baseColor);
        }
    }
}
