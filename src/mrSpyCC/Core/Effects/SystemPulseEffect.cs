using System;
using System.Runtime.InteropServices;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 💓 Donanım Telemetri Nabzı (Hardware Telemetry Pulse)
/// Win32 GetSystemTimes ile gerçek zamanlı CPU yükü telemetrisi;
/// işlemci yüküne göre hızlanan ve renk değiştiren kalp atışı dalgaları.
/// </summary>
public sealed class SystemPulseEffect : EffectBase
{
    public override string Id => "system_pulse";
    public override string DisplayName => "Donanım Telemetri Nabzı";

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint DateTimeLow;
        public uint DateTimeHigh;
        public readonly ulong ToUInt64() => ((ulong)DateTimeHigh << 32) | DateTimeLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;

    private double _cpuUsage = 0.15;
    private double _sampleTimer;
    private double _phase;

    private const double CENTER_X = 10.5;
    private const double CENTER_Y = 2.5;

    public double CurrentCpuPercentage => _cpuUsage * 100.0;

    public override void Reset()
    {
        _phase = 0;
        _sampleTimer = 0;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdleTime = idle.ToUInt64();
            _prevKernelTime = kernel.ToUInt64();
            _prevUserTime = user.ToUInt64();
        }
    }

    public override void Update(double dt)
    {
        _sampleTimer += dt;

        if (_sampleTimer >= 0.5)
        {
            _sampleTimer = 0;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                ulong currentIdle = idle.ToUInt64();
                ulong currentKernel = kernel.ToUInt64();
                ulong currentUser = user.ToUInt64();

                ulong diffIdle = currentIdle - _prevIdleTime;
                ulong diffKernel = currentKernel - _prevKernelTime;
                ulong diffUser = currentUser - _prevUserTime;
                ulong total = diffKernel + diffUser;

                if (total > 0 && total >= diffIdle)
                {
                    double currentLoad = (double)(total - diffIdle) / total;
                    _cpuUsage = (_cpuUsage * 0.6) + (currentLoad * 0.4);
                }

                _prevIdleTime = currentIdle;
                _prevKernelTime = currentKernel;
                _prevUserTime = currentUser;
            }
        }

        double bps = 0.75 + (_cpuUsage * 1.65);
        _phase += dt * bps * 2.0 * Math.PI;
    }

    public override void Render(FrameBuffer buffer)
    {
        double cycle = _phase % (2.0 * Math.PI);
        double pulse;

        if (cycle < 0.6)
        {
            pulse = Math.Sin((cycle / 0.6) * Math.PI);
        }
        else if (cycle >= 0.8 && cycle < 1.3)
        {
            pulse = Math.Sin(((cycle - 0.8) / 0.5) * Math.PI) * 0.75;
        }
        else
        {
            pulse = 0.05;
        }

        ColorRgb baseHeatColor;
        if (_cpuUsage < 0.25)
            baseHeatColor = new ColorRgb(0, 240, 255);
        else if (_cpuUsage < 0.60)
            baseHeatColor = new ColorRgb(168, 85, 247);
        else if (_cpuUsage < 0.85)
            baseHeatColor = new ColorRgb(245, 158, 11);
        else
            baseHeatColor = new ColorRgb(239, 68, 68);

        foreach (var key in KeyMapTable.AllKeys)
        {
            double dx = key.Col - CENTER_X;
            double dy = (key.Row - CENTER_Y) * 1.8;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            double wavePhase = dist * 0.45;
            double keyPulse = Math.Clamp(pulse - (wavePhase * 0.15), 0.05, 1.0);

            byte r = (byte)(baseHeatColor.R * keyPulse);
            byte g = (byte)(baseHeatColor.G * keyPulse);
            byte b = (byte)(baseHeatColor.B * keyPulse);

            buffer.SetKey(key.Offset, new ColorRgb(r, g, b));
        }
    }
}
