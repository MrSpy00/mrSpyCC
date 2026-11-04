using mrSpyCC.Core.Engine;

namespace mrSpyCC.Core.Effects;

public sealed class OffEffect : EffectBase
{
    public override string Id => "off";
    public override string DisplayName => "Kapalı (Lights Off / Blackout)";

    public override void Update(double dt)
    {
        // No animations running
    }

    public override void Render(FrameBuffer buffer)
    {
        buffer.Clear();
    }
}
