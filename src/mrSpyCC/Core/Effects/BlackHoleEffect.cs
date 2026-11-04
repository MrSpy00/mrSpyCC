using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🕳️ Kütleçekimsel Karadelik & Süpernova (Black Hole & Supernova)
/// Merkez tekillik çekimi, olay ufku çöküşü ve periyodik süpernova patlaması.
/// </summary>
public sealed class BlackHoleEffect : EffectBase
{
    public override string Id => "black_hole";
    public override string DisplayName => "Kütleçekimsel Karadelik";

    private double _time;
    private double _cycleTime;
    private const double CYCLE_DURATION = 9.0;
    private const double COLLAPSE_START = 6.8;
    private const double SUPERNOVA_START = 7.4;

    private const double CENTER_X = 10.5;
    private const double CENTER_Y = 2.5;

    public double Speed { get; set; } = 1.0;

    public override void Reset()
    {
        _time = 0;
        _cycleTime = 0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;
        _cycleTime = (_cycleTime + dt * Speed) % CYCLE_DURATION;
    }

    public override void Render(FrameBuffer buffer)
    {
        bool isSupernova = _cycleTime >= SUPERNOVA_START;
        bool isCollapse = _cycleTime >= COLLAPSE_START && !isSupernova;

        double supernovaProgress = isSupernova ? (_cycleTime - SUPERNOVA_START) / (CYCLE_DURATION - SUPERNOVA_START) : 0.0;
        double supernovaRadius = supernovaProgress * 25.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dx = key.Col - CENTER_X;
            double dy = (key.Row - CENTER_Y) * 1.8;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);

            if (isSupernova)
            {
                double waveDist = Math.Abs(dist - supernovaRadius);
                if (waveDist < 2.5)
                {
                    double intensity = (1.0 - waveDist / 2.5) * (1.0 - supernovaProgress * 0.7);
                    byte r = (byte)(255 * intensity);
                    byte g = (byte)(235 * intensity);
                    byte b = (byte)(180 * intensity);
                    buffer.SetKey(key.Offset, new ColorRgb(r, g, b));
                    continue;
                }
            }
            else if (isCollapse)
            {
                double collapseFactor = 1.0 - (_cycleTime - COLLAPSE_START) / (SUPERNOVA_START - COLLAPSE_START);
                if (dist < 4.0 || dist > 4.0 + (1.0 - collapseFactor) * 15.0)
                {
                    buffer.SetKey(key.Offset, ColorRgb.Black);
                    continue;
                }
            }

            if (dist < 1.3)
            {
                buffer.SetKey(key.Offset, ColorRgb.Black);
                continue;
            }

            double swirlSpeed = 2.5 / Math.Max(0.5, Math.Sqrt(dist));
            double swirlPhase = angle + (_time * swirlSpeed);

            double spiralVal = Math.Sin(swirlPhase * 3.0 - dist * 0.8);
            double brightness = Math.Clamp((spiralVal + 1.0) * 0.5, 0.0, 1.0);

            double temp = Math.Clamp(1.0 - (dist / 14.0), 0.0, 1.0);

            byte red = (byte)(Math.Clamp((temp * 2.0) * brightness, 0.0, 1.0) * 255);
            byte green = (byte)(Math.Clamp((Math.Sin(temp * Math.PI) * 0.8) * brightness, 0.0, 1.0) * 220);
            byte blue = (byte)(Math.Clamp((1.0 - temp) * brightness, 0.0, 1.0) * 255);

            buffer.SetKey(key.Offset, new ColorRgb(red, green, blue));
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _cycleTime = Math.Min(_cycleTime + 0.4, CYCLE_DURATION - 0.1);
        }
    }
}
