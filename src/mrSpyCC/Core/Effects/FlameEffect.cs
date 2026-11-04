using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class FlameEffect : EffectBase
{
    public override string Id => "flame";
    public override string DisplayName => "Alev & Ateş (Flame Embers)";

    public enum FlameType
    {
        Hellfire,    // Klasik Cehennem Ateşi (Kırmızı - Turuncu - Altın Sarısı - Beyaz)
        IcePlasma,   // Buz & Plazma Ateşi (Gece Mavisi - Camgöbeği - Beyaz)
        ToxicDragon  // Ejderha Ateşi (Zümrüt Yeşili - Limon Sarısı - Beyaz)
    }

    public FlameType Type { get; set; } = FlameType.Hellfire;
    public double Speed { get; set; } = 1.6;
    public double Turbulence { get; set; } = 1.0;
    public double SparkIntensity { get; set; } = 0.85;

    // Per-key heat simulation for smooth convection
    private readonly double[] _keyHeat = new double[512];
    private readonly List<EmberParticle> _embers = new();
    private readonly Random _rnd = new();
    private double _time;

    private sealed class EmberParticle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vy { get; set; }
        public double Vx { get; set; }
        public double Life { get; set; }
        public double MaxLife { get; set; }
        public double Brightness { get; set; }
    }

    public override void Reset()
    {
        Array.Clear(_keyHeat, 0, _keyHeat.Length);
        _embers.Clear();
        _time = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;

        // 1. Thermodynamic heat update for each physical key
        double alpha = Math.Clamp(dt * Speed * 4.0, 0.05, 0.8);
        double volatility = 35.0 * Turbulence;

        foreach (var key in KeyMapTable.AllKeys)
        {
            // Bottom rows (Row 4, 5, 6) generate the core flame heat
            double rowFactor = (key.Y / 6.0); // 0 at top, 1 at bottom
            double baseHeat = 30.0 + (rowFactor * 180.0);

            // Perlin-like pseudo flame flicker noise
            double flicker = Math.Sin(key.X * 1.5 + _time * 3.2) * Math.Cos(key.Y * 2.0 - _time * 4.5);
            double randomFluctuation = (_rnd.NextDouble() - 0.5) * volatility;

            double targetHeat = baseHeat + (flicker * 35.0) + randomFluctuation;
            targetHeat = Math.Clamp(targetHeat, 0.0, 255.0);

            _keyHeat[key.Offset] = (_keyHeat[key.Offset] * (1.0 - alpha)) + (targetHeat * alpha);
        }

        // 2. Spawn and update rising flame embers (kıvılcımlar ve parıltılar)
        if (_rnd.NextDouble() < SparkIntensity)
        {
            double spawnX = 0.5 + _rnd.NextDouble() * 21.0;
            double spawnY = 4.5 + _rnd.NextDouble() * 1.5;
            double life = 0.4 + _rnd.NextDouble() * 0.7;

            _embers.Add(new EmberParticle
            {
                X = spawnX,
                Y = spawnY,
                Vx = (_rnd.NextDouble() - 0.5) * 1.4,
                Vy = -(2.5 + _rnd.NextDouble() * 3.5) * Speed,
                Life = life,
                MaxLife = life,
                Brightness = 0.8 + _rnd.NextDouble() * 0.4
            });
        }

        // Advance embers upwards with sway
        for (int i = _embers.Count - 1; i >= 0; i--)
        {
            var emb = _embers[i];
            emb.Life -= dt * 1.5;
            emb.Y += emb.Vy * dt;
            emb.X += (emb.Vx + Math.Sin(_time * 6.0 + emb.Y) * 0.8) * dt;

            if (emb.Life <= 0.0 || emb.Y < -0.5)
            {
                _embers.RemoveAt(i);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // 1. Render base convection flame
        foreach (var key in KeyMapTable.AllKeys)
        {
            double h = _keyHeat[key.Offset];
            ColorRgb col = MapHeatToColor(h, Type);
            buffer.SetKey(key.Offset, col);
        }

        // 2. Render bright burning embers and sparks
        foreach (var emb in _embers)
        {
            double progress = emb.Life / emb.MaxLife;
            double alpha = Math.Clamp(progress * emb.Brightness, 0.0, 1.0);

            ColorRgb emberColor = Type switch
            {
                FlameType.IcePlasma => ColorRgb.Lerp(new ColorRgb(0, 240, 255), ColorRgb.White, alpha),
                FlameType.ToxicDragon => ColorRgb.Lerp(new ColorRgb(120, 255, 0), ColorRgb.White, alpha),
                _ => ColorRgb.Lerp(new ColorRgb(255, 180, 20), ColorRgb.White, alpha)
            };

            foreach (var key in KeyMapTable.AllKeys)
            {
                double dx = key.X - emb.X;
                double dy = key.Y - emb.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 1.2)
                {
                    double weight = (1.2 - dist) / 1.2 * alpha;
                    buffer.BlendKey(key.Offset, emberColor, weight);
                }
            }
        }
    }

    private static ColorRgb MapHeatToColor(double heat, FlameType type)
    {
        heat = Math.Clamp(heat, 0.0, 255.0);

        if (type == FlameType.Hellfire)
        {
            if (heat < 60)
            {
                // Deep coal black to dark ember red
                double f = heat / 60.0;
                return ColorRgb.Lerp(new ColorRgb(10, 0, 0), new ColorRgb(160, 10, 0), f);
            }
            if (heat < 130)
            {
                // Dark red to vivid fire orange
                double f = (heat - 60.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(160, 10, 0), new ColorRgb(255, 100, 0), f);
            }
            if (heat < 200)
            {
                // Fire orange to brilliant golden flame yellow
                double f = (heat - 130.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(255, 100, 0), new ColorRgb(255, 220, 20), f);
            }
            // Blazing white hot core
            double wf = (heat - 200.0) / 55.0;
            return ColorRgb.Lerp(new ColorRgb(255, 220, 20), ColorRgb.White, wf);
        }
        else if (type == FlameType.IcePlasma)
        {
            if (heat < 60)
            {
                double f = heat / 60.0;
                return ColorRgb.Lerp(new ColorRgb(0, 5, 20), new ColorRgb(0, 40, 180), f);
            }
            if (heat < 130)
            {
                double f = (heat - 60.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(0, 40, 180), new ColorRgb(0, 200, 255), f);
            }
            if (heat < 200)
            {
                double f = (heat - 130.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(0, 200, 255), new ColorRgb(180, 240, 255), f);
            }
            double wf = (heat - 200.0) / 55.0;
            return ColorRgb.Lerp(new ColorRgb(180, 240, 255), ColorRgb.White, wf);
        }
        else // ToxicDragon
        {
            if (heat < 60)
            {
                double f = heat / 60.0;
                return ColorRgb.Lerp(new ColorRgb(0, 15, 0), new ColorRgb(0, 140, 20), f);
            }
            if (heat < 130)
            {
                double f = (heat - 60.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(0, 140, 20), new ColorRgb(120, 255, 0), f);
            }
            if (heat < 200)
            {
                double f = (heat - 130.0) / 70.0;
                return ColorRgb.Lerp(new ColorRgb(120, 255, 0), new ColorRgb(220, 255, 80), f);
            }
            double wf = (heat - 200.0) / 55.0;
            return ColorRgb.Lerp(new ColorRgb(220, 255, 80), ColorRgb.White, wf);
        }
    }
}
