using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class BreathingEffect : EffectBase
{
    public override string Id => "breathing";
    public override string DisplayName => "Nefes (Breathing)";

    public double Speed { get; set; } = 1.0;
    public bool MultiColorMode { get; set; } = true;
    public ColorRgb PrimaryColor { get; set; } = ColorRgb.Cyan;

    private double _time;
    private int _paletteIdx;

    public override void Reset()
    {
        _time = 0.0;
        _paletteIdx = 0;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;
        if (_time >= Math.PI * 2.0)
        {
            _time -= Math.PI * 2.0;
            _paletteIdx = (_paletteIdx + 1) % RainbowPalette.Length;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Smooth ease-in-out cosine pulse: 0.0 to 1.0
        double factor = 0.5 * (1.0 - Math.Cos(_time));
        // Gamma correction for natural eye perception
        factor = Math.Pow(factor, 1.8);

        ColorRgb targetColor = MultiColorMode ? RainbowPalette[_paletteIdx] : PrimaryColor;
        ColorRgb activeColor = targetColor.Scale(factor);

        foreach (var key in KeyMapTable.AllKeys)
        {
            buffer.SetKey(key.Offset, activeColor);
        }
    }
}
