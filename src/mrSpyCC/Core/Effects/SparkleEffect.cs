using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class SparkleEffect : EffectBase
{
    public override string Id => "sparkle";
    public override string DisplayName => "Parıltı (Sparkle)";

    public double Speed { get; set; } = 1.2;
    public double Density { get; set; } = 0.35; // Fraction of keys glittering
    public ColorRgb SparkleColor { get; set; } = ColorRgb.White;
    public ColorRgb BackgroundColor { get; set; } = new(10, 15, 30);
    public bool RainbowSparkle { get; set; } = true;

    private readonly double[] _phases = new double[512];
    private readonly double[] _speeds = new double[512];
    private readonly double[] _hues = new double[512];
    private readonly Random _rnd = new();

    public override void Reset()
    {
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] = _rnd.NextDouble() * Math.PI * 2.0;
            _speeds[i] = 1.0 + _rnd.NextDouble() * 3.0;
            _hues[i] = _rnd.Next(0, 360);
        }
    }

    public override void Update(double dt)
    {
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] += dt * _speeds[i] * Speed;
            if (_phases[i] >= Math.PI * 2.0)
            {
                _phases[i] -= Math.PI * 2.0;
                _hues[i] = _rnd.Next(0, 360);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        buffer.Fill(BackgroundColor);

        foreach (var key in KeyMapTable.AllKeys)
        {
            int off = key.Offset;
            double sin = Math.Sin(_phases[off]);
            // Only flash above threshold determined by density
            double threshold = 1.0 - (Density * 1.5);
            if (sin > threshold)
            {
                double intensity = (sin - threshold) / (1.0 - threshold);
                ColorRgb clr = RainbowSparkle
                    ? ColorRgb.FromHsv(_hues[off])
                    : SparkleColor;

                buffer.BlendKey(off, clr, intensity);
            }
        }
    }
}
