using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class LightningEffect : EffectBase
{
    public override string Id => "lightning";
    public override string DisplayName => "Yıldırım (Lightning)";

    public double Frequency { get; set; } = 1.0;
    public ColorRgb BoltColor { get; set; } = new(180, 220, 255);

    private readonly struct Bolt(List<int> offsets, double startTime, double duration)
    {
        public List<int> Offsets { get; } = offsets;
        public double StartTime { get; } = startTime;
        public double Duration { get; } = duration;
    }

    private readonly List<Bolt> _bolts = new();
    private readonly Random _rnd = new();
    private double _time;
    private double _flashIntensity;

    public override void Reset()
    {
        _bolts.Clear();
        _time = 0.0;
        _flashIntensity = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _flashIntensity = Math.Max(0.0, _flashIntensity - dt * 4.0);

        // Random strike
        if (_rnd.NextDouble() < dt * Frequency * 1.5)
        {
            GenerateStrike();
            _flashIntensity = 0.4;
        }

        for (int i = _bolts.Count - 1; i >= 0; i--)
        {
            if (_time - _bolts[i].StartTime > _bolts[i].Duration)
            {
                _bolts.RemoveAt(i);
            }
        }
    }

    private void GenerateStrike()
    {
        var offsets = new List<int>();
        int startCol = _rnd.Next(2, 18);
        int currentCol = startCol;

        for (int row = 0; row <= 5; row++)
        {
            // Pick a key closest to (currentCol, row)
            KeyEntry? best = null;
            double bestDist = double.MaxValue;

            foreach (var k in KeyMapTable.AllKeys)
            {
                if (k.Row == row)
                {
                    double d = Math.Abs(k.X - currentCol);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = k;
                    }
                }
            }

            if (best != null)
            {
                offsets.Add(best.Offset);
            }

            currentCol += _rnd.Next(-1, 2);
        }

        _bolts.Add(new Bolt(offsets, _time, 0.45));
    }

    public override void Render(FrameBuffer buffer)
    {
        if (_flashIntensity > 0.01)
        {
            buffer.Fill(new ColorRgb(20, 30, 60).Scale(_flashIntensity));
        }

        foreach (var bolt in _bolts)
        {
            double age = _time - bolt.StartTime;
            double alpha = Math.Clamp(1.0 - (age / bolt.Duration), 0.0, 1.0);

            foreach (int offset in bolt.Offsets)
            {
                buffer.BlendKey(offset, BoltColor, alpha);
            }
        }
    }
}
