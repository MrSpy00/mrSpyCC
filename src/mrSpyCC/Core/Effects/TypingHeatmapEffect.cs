using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class TypingHeatmapEffect : EffectBase
{
    public override string Id => "typing_heatmap";
    public override string DisplayName => "Yazı Isı Haritası (Typing Heatmap)";

    public double CoolingRate { get; set; } = 0.35; // Heat decays per second
    public double CurrentWpm { get; private set; }
    public long TotalKeystrokes { get; private set; }

    private readonly double[] _heat = new double[512];
    private readonly Queue<double> _keystrokeTimestamps = new();
    private readonly object _lock = new();
    private double _elapsedTime;

    public override void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_heat, 0, _heat.Length);
            _keystrokeTimestamps.Clear();
            _elapsedTime = 0.0;
            CurrentWpm = 0.0;
            TotalKeystrokes = 0;
        }
    }

    public void RecordGlobalKeystroke()
    {
        lock (_lock)
        {
            double now = Environment.TickCount64 * 0.001;
            _keystrokeTimestamps.Enqueue(now);
            TotalKeystrokes++;
        }
    }

    public void RefreshWpm()
    {
        lock (_lock)
        {
            double now = Environment.TickCount64 * 0.001;
            while (_keystrokeTimestamps.Count > 0 && now - _keystrokeTimestamps.Peek() > 5.0)
            {
                _keystrokeTimestamps.Dequeue();
            }

            double keysPerMinute = (_keystrokeTimestamps.Count / 5.0) * 60.0;
            CurrentWpm = Math.Round(keysPerMinute / 5.0);
        }
    }

    public override void Update(double dt)
    {
        lock (_lock)
        {
            _elapsedTime += dt;

            // Decay heat
            double factor = Math.Clamp(1.0 - (CoolingRate * dt), 0.0, 1.0);
            for (int i = 0; i < _heat.Length; i++)
            {
                if (_heat[i] > 0.001)
                {
                    _heat[i] *= factor;
                }
                else
                {
                    _heat[i] = 0.0;
                }
            }

            double now = Environment.TickCount64 * 0.001;
            while (_keystrokeTimestamps.Count > 0 && now - _keystrokeTimestamps.Peek() > 5.0)
            {
                _keystrokeTimestamps.Dequeue();
            }

            double keysPerMinute = (_keystrokeTimestamps.Count / 5.0) * 60.0;
            CurrentWpm = Math.Round(keysPerMinute / 5.0); // Standard 5 chars = 1 word
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        lock (_lock)
        {
            double now = Environment.TickCount64 * 0.001;
            _keystrokeTimestamps.Enqueue(now);
            TotalKeystrokes++;

            // Inject heat into pressed key and slight ambient bleeding to neighbors
            int off = key.Offset;
            if (off >= 0 && off < _heat.Length)
            {
                _heat[off] = Math.Clamp(_heat[off] + 0.35, 0.0, 1.0);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        lock (_lock)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                double h = _heat[key.Offset];
                ColorRgb color = MapHeatToColor(h);
                buffer.SetKey(key.Offset, color);
            }
        }
    }

    private static ColorRgb MapHeatToColor(double heat)
    {
        heat = Math.Clamp(heat, 0.0, 1.0);

        if (heat < 0.01)
            return new ColorRgb(12, 16, 28); // Cold resting dark navy

        if (heat < 0.25)
            return ColorRgb.Lerp(new ColorRgb(12, 16, 28), new ColorRgb(124, 58, 237), heat / 0.25); // Violet

        if (heat < 0.6)
            return ColorRgb.Lerp(new ColorRgb(124, 58, 237), new ColorRgb(236, 72, 153), (heat - 0.25) / 0.35); // Pink

        if (heat < 0.85)
            return ColorRgb.Lerp(new ColorRgb(236, 72, 153), new ColorRgb(245, 158, 11), (heat - 0.6) / 0.25); // Amber

        return ColorRgb.Lerp(new ColorRgb(245, 158, 11), ColorRgb.White, (heat - 0.85) / 0.15); // White hot
    }
}
