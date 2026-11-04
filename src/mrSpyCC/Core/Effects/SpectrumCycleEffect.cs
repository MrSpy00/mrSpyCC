using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class SpectrumCycleEffect : EffectBase
{
    public override string Id => "spectrum_cycle";
    public override string DisplayName => "Spektrum Döngüsü (Spectrum Cycle)";

    public double Speed { get; set; } = 0.5;

    private double _hue;

    public override void Reset()
    {
        _hue = 0.0;
    }

    public override void Update(double dt)
    {
        _hue += dt * Speed * 60.0;
        _hue = (_hue % 360.0 + 360.0) % 360.0;
    }

    public override void Render(FrameBuffer buffer)
    {
        var color = ColorRgb.FromHsv(_hue);
        foreach (var key in KeyMapTable.AllKeys)
        {
            buffer.SetKey(key.Offset, color);
        }
    }
}
