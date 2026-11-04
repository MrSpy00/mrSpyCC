using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public abstract class EffectBase : IEffect
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }

    public virtual void Reset() { }
    public abstract void Update(double dt);
    public abstract void Render(FrameBuffer buffer);
    public virtual void OnKeyActivity(KeyEntry key, bool isDown) { }

    protected static readonly ColorRgb[] RainbowPalette =
    [
        ColorRgb.Red,
        ColorRgb.Orange,
        ColorRgb.Yellow,
        ColorRgb.Green,
        ColorRgb.Cyan,
        ColorRgb.Blue,
        ColorRgb.Magenta
    ];

    protected static readonly ColorRgb[] CyberpunkPalette =
    [
        new(0, 240, 255),   // Neon Cyan
        new(255, 0, 128),   // Hot Pink
        new(160, 32, 240),  // Deep Violet
        new(0, 255, 128)    // Neon Green
    ];

    protected static readonly ColorRgb[] SunsetPalette =
    [
        new(255, 50, 0),    // Dark Orange
        new(255, 120, 0),   // Bright Orange
        new(255, 200, 0),   // Golden Yellow
        new(180, 0, 100)    // Twilight Pink
    ];

    protected static readonly ColorRgb[] OceanPalette =
    [
        new(0, 50, 150),    // Deep Blue
        new(0, 150, 255),   // Azure
        new(0, 255, 200),   // Aqua
        new(150, 255, 255)  // Ice Cyan
    ];
}
