using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Engine;

public interface IEffect
{
    string Id { get; }
    string DisplayName { get; }

    void Reset();
    void Update(double dt);
    void Render(FrameBuffer buffer);
    void OnKeyActivity(KeyEntry key, bool isDown);
}
