using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class GamingModeEffect : EffectBase
{
    public override string Id => "gaming_mode";
    public override string DisplayName => "Oyun Modu (Gaming Mode - WASD & Macro)";

    public enum GamePreset
    {
        FpsCompetitive,   // WASD, Shift, Space, R, E, Q, F, 1-5
        MobaStrategy,     // Q, W, E, R, D, F, B, 1-6, Space, Tab
        ArrowKeysDriving  // Arrow keys, Space, Shift, WASD
    }

    public GamePreset Preset { get; set; } = GamePreset.FpsCompetitive;
    public ColorRgb WasdColor { get; set; } = new(124, 58, 237);      // aegisSoft Purple
    public ColorRgb ActionKeyColor { get; set; } = new(0, 240, 255);  // Neon Cyan
    public ColorRgb BackgroundColor { get; set; } = new(10, 12, 18);  // Stealth dim
    public bool ReactiveKeyPulse { get; set; } = true;

    private readonly Dictionary<int, double> _hitKeys = new();
    private readonly List<int> _keysToRemoveBuffer = new(16);
    private readonly object _lock = new();
    private double _time;

    public override void Reset()
    {
        _time = 0.0;
        lock (_lock)
        {
            _hitKeys.Clear();
        }
    }

    public override void Update(double dt)
    {
        _time += dt;
        lock (_lock)
        {
            if (_hitKeys.Count > 0)
            {
                _keysToRemoveBuffer.Clear();
                foreach (var kvp in _hitKeys)
                {
                    if (_time - kvp.Value > 0.6)
                    {
                        _keysToRemoveBuffer.Add(kvp.Key);
                    }
                }
                for (int i = 0; i < _keysToRemoveBuffer.Count; i++)
                {
                    _hitKeys.Remove(_keysToRemoveBuffer[i]);
                }
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown || !ReactiveKeyPulse) return;
        lock (_lock)
        {
            _hitKeys[key.Offset] = _time;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        lock (_lock)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                ColorRgb keyCol = BackgroundColor;

                if (IsWasd(key))
                {
                    keyCol = WasdColor;
                }
                else if (IsActionKey(key, Preset))
                {
                    keyCol = ActionKeyColor;
                }

                if (_hitKeys.TryGetValue(key.Offset, out double hitTime))
                {
                    double age = _time - hitTime;
                    double flash = Math.Max(0.0, 1.0 - (age / 0.6));
                    keyCol = ColorRgb.Lerp(keyCol, ColorRgb.White, flash);
                }

                buffer.SetKey(key.Offset, keyCol);
            }
        }
    }

    private static bool IsWasd(KeyEntry key)
    {
        return key.Group is KeyGroup.Wasd or KeyGroup.Arrows
            || key.Name is "W" or "A" or "S" or "D" or "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight";
    }

    private static bool IsActionKey(KeyEntry key, GamePreset preset)
    {
        return preset switch
        {
            GamePreset.MobaStrategy => key.Name is "Q" or "E" or "R" or "D" or "F" or "B" or "Space" or "Tab"
                                           or "1" or "2" or "3" or "4" or "5" or "6",
            GamePreset.ArrowKeysDriving => key.Name is "Space" or "ShiftL" or "CtrlL" or "ShiftR" or "LShift" or "LCtrl" or "RShift" or "RCtrl" or "Numpad0",
            _ => key.Name is "ShiftL" or "LShift" or "Space" or "CtrlL" or "LCtrl" or "Tab" or "R" or "E" or "Q" or "F" or "C" or "G" or "1" or "2" or "3" or "4" or "5"
        };
    }
}
