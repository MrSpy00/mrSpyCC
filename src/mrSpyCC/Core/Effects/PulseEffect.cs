using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class PulseEffect : EffectBase
{
    public override string Id => "pulse";
    public override string DisplayName => "Nabız / Kalp Atışı (Pulse)";

    public double Speed { get; set; } = 1.0;
    public ColorRgb PrimaryColor { get; set; } = new(168, 85, 247); // aegisSoft Purple
    public bool HeartbeatMode { get; set; } = true;
    public bool RainbowCycle { get; set; } = false;

    private double _time;

    public override void Reset()
    {
        _time = 0.0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed * 2.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        double intensity;

        if (HeartbeatMode)
        {
            // Two sharp pulses followed by pause (Lub-Dub rhythm)
            double cycle = _time % (2.0 * Math.PI);
            if (cycle < 0.6)
            {
                // First thump
                intensity = Math.Sin(cycle * (Math.PI / 0.6));
            }
            else if (cycle >= 0.8 && cycle < 1.4)
            {
                // Second thump (slightly softer)
                intensity = 0.75 * Math.Sin((cycle - 0.8) * (Math.PI / 0.6));
            }
            else
            {
                // Diastole pause
                intensity = 0.05;
            }
        }
        else
        {
            // Smooth harmonic pulse
            intensity = 0.5 + 0.5 * Math.Sin(_time);
        }

        intensity = Math.Clamp(intensity, 0.05, 1.0);

        ColorRgb color = RainbowCycle
            ? ColorRgb.FromHsv((_time * 40.0) % 360.0)
            : PrimaryColor;

        ColorRgb rendered = color.Scale(intensity);

        foreach (var key in KeyMapTable.AllKeys)
        {
            buffer.SetKey(key.Offset, rendered);
        }
    }
}
