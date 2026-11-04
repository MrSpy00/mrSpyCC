using System;
using mrSpyCC.Core.Audio;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class AudioVisualizerEffect : EffectBase
{
    public override string Id => "audio_visualizer";
    public override string DisplayName => "Ses Görselleştirici (Audio Visualizer)";

    public enum VisualizerMode
    {
        EqualizerBars,
        BassPulse,
        WaveEnergy
    }

    public VisualizerMode Mode { get; set; } = VisualizerMode.EqualizerBars;
    public double Sensitivity { get; set; } = 1.8;
    public int PaletteIndex { get; set; } = 1; // 0=Rainbow, 1=Cyberpunk, 2=Sunset

    private readonly WasapiAudioCapture _audioCapture;
    private readonly float[] _bands = new float[16];
    private double _time;

    public AudioVisualizerEffect(WasapiAudioCapture audioCapture)
    {
        _audioCapture = audioCapture;
    }

    public override void Reset()
    {
        _time = 0.0;
        Array.Clear(_bands, 0, _bands.Length);
    }

    public override void Update(double dt)
    {
        _time += dt;
        _audioCapture.GetFrequencyBands(_bands);
    }

    public override void Render(FrameBuffer buffer)
    {
        var palette = PaletteIndex switch
        {
            1 => CyberpunkPalette,
            2 => SunsetPalette,
            _ => RainbowPalette
        };

        if (Mode == VisualizerMode.EqualizerBars)
        {
            // Map 16 bands across the keyboard X columns (X ranges from 0 to 20)
            const double maxDeckX = 20.5;

            foreach (var key in KeyMapTable.AllKeys)
            {
                int bandIndex = Math.Clamp((int)((key.X / maxDeckX) * _bands.Length), 0, _bands.Length - 1);
                float magnitude = Math.Clamp(_bands[bandIndex] * (float)Sensitivity, 0f, 1f);

                // Row height mapping: Bottom rows light up first, top rows for peaks
                // Rows range from 0 (top/F-keys) to 5/6 (bottom)
                double invertedY = 1.0 - (key.Y / 6.0); // 0.0 at top, 1.0 at bottom
                if (invertedY <= magnitude)
                {
                    double colorPos = (bandIndex / (double)_bands.Length) * (palette.Length - 1);
                    int cIdx1 = (int)colorPos;
                    int cIdx2 = Math.Min(cIdx1 + 1, palette.Length - 1);
                    var color = ColorRgb.Lerp(palette[cIdx1], palette[cIdx2], colorPos - cIdx1);

                    buffer.SetKey(key.Offset, color);
                }
            }
        }
        else if (Mode == VisualizerMode.BassPulse)
        {
            float bass = Math.Clamp(_audioCapture.BassLevel * (float)Sensitivity, 0f, 1f);
            const double centerX = 10.0;
            const double centerY = 3.0;
            double maxDist = 12.0;

            foreach (var key in KeyMapTable.AllKeys)
            {
                double dx = key.X - centerX;
                double dy = key.Y - centerY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double threshold = bass * maxDist;
                if (dist <= threshold)
                {
                    double intensity = 1.0 - (dist / threshold);
                    var color = ColorRgb.Lerp(palette[0], palette[palette.Length - 1], intensity);
                    buffer.SetKey(key.Offset, color.Scale(intensity));
                }
            }
        }
        else if (Mode == VisualizerMode.WaveEnergy)
        {
            float bass = _audioCapture.BassLevel;
            double speed = 1.0 + bass * 3.0;
            double hueOffset = _time * 50.0 * speed;

            foreach (var key in KeyMapTable.AllKeys)
            {
                double hue = (hueOffset + key.X * 15.0 + key.Y * 10.0) % 360.0;
                var color = ColorRgb.FromHsv(hue).Scale(0.3 + bass * 0.7);
                buffer.SetKey(key.Offset, color);
            }
        }
    }
}
