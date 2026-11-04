using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class StrobeEffect : EffectBase
{
    public override string Id => "strobe";
    public override string DisplayName => "Flaş (Strobe)";

    public double FrequencyHz { get; set; } = 8.0; // 1 to 15 Hz
    public ColorRgb FlashColor { get; set; } = ColorRgb.White;
    public bool AlternateColor { get; set; } = false;
    public ColorRgb SecondaryColor { get; set; } = new(124, 58, 237); // aegisSoft Purple

    private double _time;
    private bool _isAlt;

    public override void Reset()
    {
        _time = 0.0;
        _isAlt = false;
    }

    public override void Update(double dt)
    {
        _time += dt;
        double period = 1.0 / Math.Clamp(FrequencyHz, 0.5, 15.0);
        if (_time >= period)
        {
            _time -= period;
            _isAlt = !_isAlt;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        double period = 1.0 / Math.Clamp(FrequencyHz, 0.5, 15.0);
        // Duty cycle: 35% on, 65% off
        bool isOn = _time < (period * 0.35);

        if (!isOn)
        {
            buffer.Clear();
            return;
        }

        ColorRgb activeColor = (AlternateColor && _isAlt) ? SecondaryColor : FlashColor;

        foreach (var key in KeyMapTable.AllKeys)
        {
            buffer.SetKey(key.Offset, activeColor);
        }
    }
}
