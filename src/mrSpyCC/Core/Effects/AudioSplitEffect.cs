using System;
using mrSpyCC.Core.Audio;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🎛️ Frekans Ayrışımlı Ses Reaktif 2.0 (Audio Frequency Split)
/// Ses spektrumunu fiziksel satırlara böler:
/// Alt Satır: Derin Bas (Sub-Bass) vuruşları (Magenta/Kızıl)
/// Orta Satırlar: Vokal ve Melodi akışı (Camgöbeği/Aqua)
/// Üst Satırlar (F-Tuşları): Tiz ziller ve parıltılar (Altın/Beyaz)
/// </summary>
public sealed class AudioSplitEffect : EffectBase
{
    public override string Id => "audio_split";
    public override string DisplayName => "Frekans Ayrışımlı Ses 2.0";

    private readonly WasapiAudioCapture _audioCapture;
    private readonly float[] _bands = new float[16];
    private double _time;

    public double Sensitivity { get; set; } = 2.0;

    public AudioSplitEffect(WasapiAudioCapture audioCapture)
    {
        _audioCapture = audioCapture;
    }

    public override void Reset()
    {
        _time = 0;
        Array.Clear(_bands, 0, _bands.Length);
    }

    public override void Update(double dt)
    {
        _time += dt;
        _audioCapture.GetFrequencyBands(_bands);
    }

    public override void Render(FrameBuffer buffer)
    {
        float bass = 0f;
        for (int i = 0; i <= 3; i++) bass += _bands[i];
        bass = Math.Clamp((bass / 4.0f) * (float)Sensitivity, 0f, 1f);

        float mid = 0f;
        for (int i = 4; i <= 10; i++) mid += _bands[i];
        mid = Math.Clamp((mid / 7.0f) * (float)Sensitivity, 0f, 1f);

        float treble = 0f;
        for (int i = 11; i <= 15; i++) treble += _bands[i];
        treble = Math.Clamp((treble / 5.0f) * (float)Sensitivity, 0f, 1f);

        foreach (var key in KeyMapTable.AllKeys)
        {
            int row = key.Row;

            ColorRgb keyColor;

            if (row == 5)
            {
                byte r = (byte)(bass * 255);
                byte g = (byte)(bass * 20);
                byte b = (byte)(bass * 140);
                keyColor = new ColorRgb(r, g, b);
            }
            else if (row >= 2 && row <= 4)
            {
                double wave = Math.Sin((key.Col * 0.4) + (_time * 4.0)) * 0.5 + 0.5;
                float intensity = Math.Clamp(mid * (float)wave * 1.3f, 0f, 1f);

                byte r = (byte)(intensity * 10);
                byte g = (byte)(intensity * 220);
                byte b = (byte)(intensity * 255);
                keyColor = new ColorRgb(r, g, b);
            }
            else
            {
                int bandIdx = Math.Clamp(key.Col % 5 + 11, 11, 15);
                float keyTreble = Math.Clamp(_bands[bandIdx] * (float)Sensitivity, 0f, 1f);

                byte r = (byte)(keyTreble * 255);
                byte g = (byte)(keyTreble * 225);
                byte b = (byte)(keyTreble * 130);
                keyColor = new ColorRgb(r, g, b);
            }

            buffer.SetKey(key.Offset, keyColor);
        }
    }
}
