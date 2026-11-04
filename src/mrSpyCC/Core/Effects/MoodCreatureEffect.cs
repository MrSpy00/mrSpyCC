using System;
using System.Runtime.InteropServices;
using mrSpyCC.Core.Audio;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class MoodCreatureEffect : EffectBase
{
    public override string Id => "mood_creature";
    public override string DisplayName => "Ruh Hali Canlısı (Mood Creature)";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    private readonly WasapiAudioCapture? _audioCapture;
    private double _time;
    private double _cpuUsage = 15.0;
    private double _cpuCheckTimer = 0;
    private long _prevIdle, _prevKernel, _prevUser;

    private double _creatureX = 10.0;
    private double _creatureY = 2.5;
    private double _audioEnergy = 0.0;

    public MoodCreatureEffect(WasapiAudioCapture? audioCapture = null)
    {
        _audioCapture = audioCapture;
    }

    public override void Reset()
    {
        _time = 0;
        _cpuCheckTimer = 0;
        _creatureX = 10.0;
        _creatureY = 2.5;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _cpuCheckTimer -= dt;

        // 1. CPU Telemetry Check (every 0.5s)
        if (_cpuCheckTimer <= 0)
        {
            _cpuCheckTimer = 0.5;
            UpdateCpuUsage();
        }

        // 2. Audio Spectrum Energy
        if (_audioCapture != null)
        {
            Span<float> bands = stackalloc float[8];
            _audioCapture.GetFrequencyBands(bands);
            float sum = 0;
            for (int i = 0; i < bands.Length; i++)
                sum += bands[i];
            _audioEnergy = Math.Min(1.0, sum * 1.8 + _audioCapture.BassLevel * 0.8);
        }
        else
        {
            _audioEnergy = 0;
        }

        // Creature slowly glides around center
        _creatureX = 10.0 + Math.Sin(_time * 0.7) * 4.0;
        _creatureY = 2.5 + Math.Cos(_time * 0.9) * 1.2;
    }

    private void UpdateCpuUsage()
    {
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            if (_prevIdle != 0)
            {
                long idleDiff = idle - _prevIdle;
                long totalDiff = (kernel - _prevKernel) + (user - _prevUser);
                if (totalDiff > 0)
                {
                    double currentCpu = 100.0 * (1.0 - ((double)idleDiff / totalDiff));
                    _cpuUsage = Math.Clamp(currentCpu, 0.0, 100.0);
                }
            }
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Creature mood state
        bool isStressed = _cpuUsage > 75.0;
        bool isDancing = _audioEnergy > 0.25;
        double speedMult = isStressed ? 3.0 : (isDancing ? 2.5 : 1.0);

        // Creature Core Colors
        ColorRgb coreColor;
        ColorRgb tentacleColor;

        if (isStressed)
        {
            // Angry Fiery Red / Orange
            coreColor = new ColorRgb(255, 30, 20);
            tentacleColor = new ColorRgb(255, 120, 0);
        }
        else if (isDancing)
        {
            // Party Neon Magenta / Electric Cyan
            double partyHue = (_time * 180.0) % 360.0;
            coreColor = ColorRgb.FromHsv(partyHue, 1.0, 1.0);
            tentacleColor = ColorRgb.FromHsv((partyHue + 90.0) % 360.0, 1.0, 0.9);
        }
        else if (_cpuUsage > 45.0)
        {
            // Active Amber / Gold
            coreColor = new ColorRgb(255, 180, 0);
            tentacleColor = new ColorRgb(200, 100, 0);
        }
        else
        {
            // Calm Bioluminescent Emerald / Teal
            coreColor = new ColorRgb(0, 255, 180);
            tentacleColor = new ColorRgb(0, 140, 220);
        }

        // Ambient dark body background
        buffer.Fill(4, 5, 10);

        double breath = Math.Sin(_time * speedMult * 3.0) * 0.4 + 1.2;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double kx = key.X + (key.Width * 0.5);
            double ky = key.Y + (key.Height * 0.5);

            double dx = kx - _creatureX;
            double dy = ky - _creatureY;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));

            // Core organism body
            if (dist < 1.4 * breath)
            {
                buffer.SetColor(key.Offset, coreColor.R, coreColor.G, coreColor.B);
            }
            // Radiating bioluminescent tentacles
            else if (dist < 5.0)
            {
                double angle = Math.Atan2(dy, dx);
                // 6 undulating arms
                double armWave = Math.Sin(angle * 6.0 + _time * speedMult * 4.0);
                if (armWave > 0.3)
                {
                    double falloff = 1.0 - (dist / 5.0);
                    byte r = (byte)(tentacleColor.R * falloff);
                    byte g = (byte)(tentacleColor.G * falloff);
                    byte b = (byte)(tentacleColor.B * falloff);
                    buffer.BlendPixel(key.Offset, r, g, b, (byte)(falloff * 200));
                }
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            // Dokunulan tuşa doğru biyo-elektrik sıçraması
            _creatureX += ((key.X + (key.Width * 0.5)) - _creatureX) * 0.4;
            _creatureY += ((key.Y + (key.Height * 0.5)) - _creatureY) * 0.4;
        }
    }
}
