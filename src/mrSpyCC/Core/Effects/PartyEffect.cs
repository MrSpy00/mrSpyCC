using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class PartyEffect : EffectBase
{
    public override string Id => "party";
    public override string DisplayName => "Parti / Disko (Party Random)";

    public double Bpm { get; set; } = 128.0;
    public int Mode { get; set; } = 0; // 0=Random Per-Key Sparkle, 1=Zone Flash, 2=Strobe Pulse

    private double _time;
    private double _beatProgress;
    private readonly Random _rnd = new();
    private readonly Dictionary<int, ColorRgb> _keyTargetColors = new();
    private ColorRgb _zone1Color = ColorRgb.Magenta;
    private ColorRgb _zone2Color = ColorRgb.Cyan;
    private ColorRgb _zone3Color = ColorRgb.Yellow;

    public override void Reset()
    {
        _time = 0.0;
        _beatProgress = 0.0;
        _keyTargetColors.Clear();
        foreach (var key in KeyMapTable.AllKeys)
        {
            _keyTargetColors[key.Offset] = ColorRgb.FromHsv(_rnd.Next(0, 360));
        }
    }

    public override void Update(double dt)
    {
        _time += dt;
        double beatInterval = 60.0 / Math.Max(30.0, Bpm);
        _beatProgress += dt / beatInterval;

        if (_beatProgress >= 1.0)
        {
            _beatProgress -= 1.0;

            // Shift colors on the beat!
            _zone1Color = ColorRgb.FromHsv(_rnd.Next(0, 360));
            _zone2Color = ColorRgb.FromHsv(_rnd.Next(0, 360));
            _zone3Color = ColorRgb.FromHsv(_rnd.Next(0, 360));

            foreach (var key in KeyMapTable.AllKeys)
            {
                if (_rnd.NextDouble() < 0.4)
                {
                    _keyTargetColors[key.Offset] = ColorRgb.FromHsv(_rnd.Next(0, 360));
                }
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        double beatDecay = Math.Pow(1.0 - _beatProgress, 1.8);

        switch (Mode)
        {
            case 1: // Zone Flash
                foreach (var key in KeyMapTable.AllKeys)
                {
                    ColorRgb zCol = (key.X < 7.0) ? _zone1Color : (key.X < 15.0) ? _zone2Color : _zone3Color;
                    buffer.SetKey(key.Offset, zCol.Scale(0.3 + 0.7 * beatDecay));
                }
                break;

            case 2: // Strobe Pulse
                ColorRgb flashCol = _zone1Color.Scale(beatDecay);
                foreach (var key in KeyMapTable.AllKeys)
                {
                    buffer.SetKey(key.Offset, flashCol);
                }
                break;

            default: // 0: Random Per-Key Sparkle
                foreach (var key in KeyMapTable.AllKeys)
                {
                    if (_keyTargetColors.TryGetValue(key.Offset, out var col))
                    {
                        buffer.SetKey(key.Offset, col.Scale(0.2 + 0.8 * beatDecay));
                    }
                }
                break;
        }
    }
}
