using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🔮 Organik Plazma & Lav Lambası (Plasma Field)
/// Çok frekanslı sürekli trigonometrik alan ve lav lambası akışı simülasyonu.
/// </summary>
public sealed class PlasmaEffect : EffectBase
{
    public override string Id => "plasma";
    public override string DisplayName => "Organik Plazma";

    private double _time;
    private double _flashIntensity;

    public double Speed { get; set; } = 1.2;
    public double Scale { get; set; } = 1.0;

    public override void Reset()
    {
        _time = 0;
        _flashIntensity = 0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;
        if (_flashIntensity > 0)
        {
            _flashIntensity = Math.Max(0, _flashIntensity - dt * 2.5);
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        double t = _time;
        double s = Scale;

        double cx = 10.5 + 6.0 * Math.Sin(t * 0.7);
        double cy = 2.5 + 2.0 * Math.Cos(t * 0.85);

        foreach (var key in KeyMapTable.AllKeys)
        {
            double x = key.Col * 0.35 * s;
            double y = key.Row * 0.75 * s;

            double v1 = Math.Sin(x + t);
            double v2 = Math.Sin(y - t * 0.9);
            double v3 = Math.Sin((x + y) * 0.7 + t * 1.1);

            double dist = Math.Sqrt(Math.Pow(key.Col - cx, 2) + Math.Pow(key.Row - cy, 2));
            double v4 = Math.Sin(dist * 0.6 - t * 1.5);

            double v = (v1 + v2 + v3 + v4) * 0.25;
            double norm = (v + 1.0) * 0.5;

            // Güvenli ve canlı renk üretimi (RGB döngüsel geçiş)
            double rVal = Math.Max(0.0, Math.Sin(norm * Math.PI)) * 255.0;
            double gVal = Math.Max(0.0, Math.Sin(norm * Math.PI + (2 * Math.PI / 3))) * 180.0;
            double bVal = Math.Max(0.0, Math.Sin(norm * Math.PI + (4 * Math.PI / 3))) * 255.0;

            if (_flashIntensity > 0)
            {
                rVal = Math.Min(255.0, rVal + _flashIntensity * 120.0);
                gVal = Math.Min(255.0, gVal + _flashIntensity * 180.0);
                bVal = Math.Min(255.0, bVal + _flashIntensity * 255.0);
            }

            byte r = (byte)Math.Clamp(rVal, 0.0, 255.0);
            byte g = (byte)Math.Clamp(gVal, 0.0, 255.0);
            byte b = (byte)Math.Clamp(bVal, 0.0, 255.0);

            buffer.SetKey(key.Offset, new ColorRgb(r, g, b));
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _flashIntensity = 1.0;
        }
    }
}
