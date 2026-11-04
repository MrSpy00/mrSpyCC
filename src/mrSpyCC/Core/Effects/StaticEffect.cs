using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class StaticEffect : EffectBase
{
    public override string Id => "static";
    public override string DisplayName => "Sabit Renk (Static Multi-Zone)";

    public enum StaticLayoutMode
    {
        UniformColor,
        ThreeZoneSplit,
        WasdHighlight
    }

    public StaticLayoutMode Mode { get; set; } = StaticLayoutMode.UniformColor;
    public ColorRgb PrimaryColor { get; set; } = new(124, 58, 237); // aegisSoft Purple
    public ColorRgb Zone2Color { get; set; } = new(0, 240, 255);    // Neon Cyan
    public ColorRgb Zone3Color { get; set; } = new(255, 0, 128);    // Hot Pink

    public override void Update(double dt)
    {
        // Static colors do not require per-frame physics computation
    }

    public override void Render(FrameBuffer buffer)
    {
        switch (Mode)
        {
            case StaticLayoutMode.ThreeZoneSplit:
                foreach (var key in KeyMapTable.AllKeys)
                {
                    ColorRgb col = (key.X < 7.0) ? PrimaryColor : (key.X < 15.0) ? Zone2Color : Zone3Color;
                    buffer.SetKey(key.Offset, col);
                }
                break;

            case StaticLayoutMode.WasdHighlight:
                foreach (var key in KeyMapTable.AllKeys)
                {
                    bool isGaming = key.Group is KeyGroup.Wasd or KeyGroup.Arrows
                        || key.Name is "W" or "A" or "S" or "D" or "Up" or "Down" or "Left" or "Right"
                        || key.Name is "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight";
                    buffer.SetKey(key.Offset, isGaming ? PrimaryColor : Zone2Color);
                }
                break;

            default: // Uniform
                foreach (var key in KeyMapTable.AllKeys)
                {
                    buffer.SetKey(key.Offset, PrimaryColor);
                }
                break;
        }
    }
}
