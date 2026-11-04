using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🧬 3D DNA Çift Sarmalı (DNA Double Helix)
/// 3 boyutta dönen iki tamamlayıcı sarmal iplikçik ve nükleotit baz köprüleri.
/// </summary>
public sealed class DnaHelixEffect : EffectBase
{
    public override string Id => "dna_helix";
    public override string DisplayName => "3D DNA Çift Sarmalı";

    private double _time;
    public double Speed { get; set; } = 1.3;

    public override void Reset() => _time = 0;

    public override void Update(double dt)
    {
        _time += dt * Speed;
    }

    public override void Render(FrameBuffer buffer)
    {
        double t = _time * 2.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double xPhase = key.Col * 0.42 + t;

            double yA = 2.5 + (1.9 * Math.Sin(xPhase));
            double yB = 2.5 - (1.9 * Math.Sin(xPhase));

            double zA = Math.Cos(xPhase);
            double zB = -zA;

            double distA = Math.Abs(key.Row - yA);
            double distB = Math.Abs(key.Row - yB);

            bool isRung = key.Col % 3 == 0;
            double minY = Math.Min(yA, yB);
            double maxY = Math.Max(yA, yB);
            bool isBetweenStrands = key.Row >= minY - 0.4 && key.Row <= maxY + 0.4;

            ColorRgb finalColor = ColorRgb.Black;

            if (distA < 0.9)
            {
                double intensity = (1.0 - (distA / 0.9)) * (0.45 + (zA + 1.0) * 0.275);
                byte r = (byte)(intensity * 0);
                byte g = (byte)(intensity * 240);
                byte b = (byte)(intensity * 255);
                finalColor = new ColorRgb(r, g, b);
            }
            else if (distB < 0.9)
            {
                double intensity = (1.0 - (distB / 0.9)) * (0.45 + (zB + 1.0) * 0.275);
                byte r = (byte)(intensity * 255);
                byte g = (byte)(intensity * 20);
                byte b = (byte)(intensity * 180);
                finalColor = new ColorRgb(r, g, b);
            }
            else if (isRung && isBetweenStrands)
            {
                bool baseType = (key.Col / 3) % 2 == 0;
                finalColor = baseType
                    ? new ColorRgb(245, 158, 11)
                    : new ColorRgb(16, 185, 129);
            }

            buffer.SetKey(key.Offset, finalColor);
        }
    }
}
