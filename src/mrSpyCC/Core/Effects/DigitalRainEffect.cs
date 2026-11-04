using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class DigitalRainEffect : EffectBase
{
    public override string Id => "digital_rain";
    public override string DisplayName => "Dijital Veri Fırtınası (Cyber Glitch Torrent)";

    public enum TorrentPalette
    {
        CyberpunkNeon,   // Neon Pembe & Camgöbeği (Hot Pink & Neon Cyan)
        SynthwavePurple, // Gece Moru & Günbatımı Altını
        ToxicGlitch      // Asit Yeşili & Elektrik Mavisi
    }

    public TorrentPalette Palette { get; set; } = TorrentPalette.CyberpunkNeon;
    public double Speed { get; set; } = 2.4;
    public double GlitchFrequency { get; set; } = 0.85;

    // High velocity diagonal packet stream
    private double _time;
    private readonly Dictionary<int, (double expiry, ColorRgb color)> _glitchedKeys = new();
    private readonly List<int> _expiredBuffer = new(16);
    private readonly Random _rnd = new();

    public override void Reset()
    {
        _time = 0.0;
        _glitchedKeys.Clear();
    }

    public override void Update(double dt)
    {
        _time += dt * Speed;

        // 1. Purge expired glitches (Zero-allocation reuse)
        if (_glitchedKeys.Count > 0)
        {
            _expiredBuffer.Clear();
            foreach (var kvp in _glitchedKeys)
            {
                if (_time > kvp.Value.expiry)
                    _expiredBuffer.Add(kvp.Key);
            }
            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                _glitchedKeys.Remove(_expiredBuffer[i]);
            }
        }

        // 2. Spawn random decipher glitch bursts on keys
        if (_rnd.NextDouble() < GlitchFrequency)
        {
            int count = 1 + _rnd.Next(0, 3);
            for (int i = 0; i < count; i++)
            {
                var randomKey = KeyMapTable.AllKeys[_rnd.Next(0, KeyMapTable.AllKeys.Count)];
                double duration = 0.08 + _rnd.NextDouble() * 0.18;
                ColorRgb glitchCol = (_rnd.NextDouble() < 0.4) ? ColorRgb.White : GetSecondaryColor();
                _glitchedKeys[randomKey.Offset] = (_time + duration, glitchCol);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        ColorRgb primary = GetPrimaryColor();
        ColorRgb secondary = GetSecondaryColor();

        // 1. Diagonal high-velocity data packets
        foreach (var key in KeyMapTable.AllKeys)
        {
            // Diagonal wave equation: X*0.7 + Y*1.4
            double diag = key.X * 0.5 + key.Y * 1.6;
            double wave = Math.Sin(diag * 2.8 - _time * 8.0);
            double packet = Math.Sin(key.X * 1.8 + Math.Cos(key.Y * 3.0 + _time * 4.0));
            double combined = (wave + packet) * 0.5;

            ColorRgb cellColor;
            if (combined > 0.4)
            {
                double intensity = (combined - 0.4) / 0.6;
                cellColor = ColorRgb.Lerp(primary, secondary, intensity).Scale(0.3 + 0.7 * intensity);
            }
            else
            {
                cellColor = new ColorRgb(6, 8, 14); // Dark stealth data ground
            }

            buffer.SetKey(key.Offset, cellColor);
        }

        // 2. Overlay bright deciphering glitch bits
        foreach (var kvp in _glitchedKeys)
        {
            buffer.SetKey(kvp.Key, kvp.Value.color);
        }
    }

    private ColorRgb GetPrimaryColor()
    {
        return Palette switch
        {
            TorrentPalette.SynthwavePurple => new ColorRgb(140, 20, 255), // Violet
            TorrentPalette.ToxicGlitch => new ColorRgb(0, 255, 120),       // Toxic Lime
            _ => new ColorRgb(0, 220, 255)                                // Neon Cyan
        };
    }

    private ColorRgb GetSecondaryColor()
    {
        return Palette switch
        {
            TorrentPalette.SynthwavePurple => new ColorRgb(255, 140, 0),  // Sunset Gold
            TorrentPalette.ToxicGlitch => new ColorRgb(0, 140, 255),      // Electric Blue
            _ => new ColorRgb(255, 0, 128)                                // Hot Pink
        };
    }
}
