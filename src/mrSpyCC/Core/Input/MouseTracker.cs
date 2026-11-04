using System;
using System.Runtime.InteropServices;

namespace mrSpyCC.Core.Input;

public static class MouseTracker
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private static int _cachedWidth = 0;
    private static int _cachedHeight = 0;
    private static long _lastMetricCheck = 0;

    /// <summary>
    /// Gets the current mouse cursor position normalized to [0.0, 1.0].
    /// </summary>
    public static void GetNormalizedPosition(out double nx, out double ny)
    {
        long now = Environment.TickCount64;
        if (_cachedWidth <= 0 || now - _lastMetricCheck > 5000)
        {
            _cachedWidth = Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
            _cachedHeight = Math.Max(1, GetSystemMetrics(SM_CYSCREEN));
            _lastMetricCheck = now;
        }

        if (GetCursorPos(out var pt))
        {
            nx = Math.Clamp((double)pt.X / _cachedWidth, 0.0, 1.0);
            ny = Math.Clamp((double)pt.Y / _cachedHeight, 0.0, 1.0);
        }
        else
        {
            nx = 0.5;
            ny = 0.5;
        }
    }

    /// <summary>
    /// Maps the mouse cursor directly onto the keyboard grid (X: 0.0..21.0, Y: 0.0..5.0).
    /// </summary>
    public static void GetKeyboardCoord(out double kx, out double ky)
    {
        GetNormalizedPosition(out var nx, out var ny);
        kx = nx * 20.5;
        ky = ny * 5.0;
    }
}
