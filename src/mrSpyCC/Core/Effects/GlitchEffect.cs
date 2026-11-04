using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 👾 Siber Glitch & CRT Raster (Glitch / Cyberpunk Cyberdeck)
/// Elektron ışını taraması, kromatik renk sapması ve anlık dijital bit bozulmaları.
/// </summary>
public sealed class GlitchEffect : EffectBase
{
    public override string Id => "glitch";
    public override string DisplayName => "Siber Glitch & CRT";

    private double _time;
    private double _glitchTimer;
    private bool _isGlitching;
    private int _glitchRow;
    private int _bitflipKeyOffset;
    private double _bitflipDuration;
    private readonly Random _rng = new();

    public double Speed { get; set; } = 1.4;

    public override void Reset()
    {
        _time = 0;
        _glitchTimer = 0;
        _isGlitching = false;
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;
        _glitchTimer += dt;

        if (_bitflipDuration > 0)
        {
            _bitflipDuration -= dt;
        }

        if (_glitchTimer > 1.2)
        {
            _glitchTimer = 0;
            _isGlitching = _rng.NextDouble() < 0.45;
            if (_isGlitching)
            {
                _glitchRow = _rng.Next(0, 6);
                int randomIdx = _rng.Next(0, KeyMapTable.AllKeys.Count);
                _bitflipKeyOffset = KeyMapTable.AllKeys[randomIdx].Offset;
                _bitflipDuration = 0.18;
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        double scanlineY = (_time * 3.5) % 6.0;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double rowNorm = key.Row / 5.0;
            byte baseR = (byte)(15 + rowNorm * 25);
            byte baseG = (byte)(5 + rowNorm * 10);
            byte baseB = (byte)(50 + rowNorm * 50);

            double distToScan = Math.Abs(key.Row - scanlineY);
            if (distToScan < 1.2)
            {
                double factor = 1.0 - (distToScan / 1.2);
                baseR = (byte)Math.Min(255, baseR + (factor * 30));
                baseG = (byte)Math.Min(255, baseG + (factor * 220));
                baseB = (byte)Math.Min(255, baseB + (factor * 255));
            }

            if (_isGlitching && key.Row == _glitchRow)
            {
                bool isLeftEdge = key.Col % 2 == 0;
                if (isLeftEdge)
                {
                    baseR = 255;
                    baseG = 0;
                    baseB = 40;
                }
                else
                {
                    baseR = 0;
                    baseG = 240;
                    baseB = 255;
                }
            }

            if (_bitflipDuration > 0 && key.Offset == _bitflipKeyOffset)
            {
                baseR = 255;
                baseG = 255;
                baseB = 100;
            }

            buffer.SetKey(key.Offset, new ColorRgb(baseR, baseG, baseB));
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _isGlitching = true;
            _glitchRow = key.Row;
            _bitflipKeyOffset = key.Offset;
            _bitflipDuration = 0.22;
        }
    }
}
