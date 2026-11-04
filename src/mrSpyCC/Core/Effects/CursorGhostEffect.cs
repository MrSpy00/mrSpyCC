using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class CursorGhostEffect : EffectBase
{
    public override string Id => "cursor_ghost";
    public override string DisplayName => "İmleç Gölgesi (Cursor Ghost)";

    private sealed class TrailNode
    {
        public double X;
        public double Y;
        public double Life = 0.55;
    }

    private readonly List<TrailNode> _trail = new();
    private double _curX = 10.0;
    private double _curY = 2.5;
    private double _time = 0;

    public override void Reset()
    {
        _trail.Clear();
        _time = 0;
    }

    public override void Update(double dt)
    {
        _time += dt;

        // Query real mouse cursor on keyboard grid
        MouseTracker.GetKeyboardCoord(out double targetX, out double targetY);

        // Smooth follower
        _curX += (targetX - _curX) * Math.Min(1.0, dt * 16.0);
        _curY += (targetY - _curY) * Math.Min(1.0, dt * 16.0);

        // Add trail node
        _trail.Add(new TrailNode { X = _curX, Y = _curY });

        // Update trail life
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            _trail[i].Life -= dt;
            if (_trail[i].Life <= 0)
                _trail.RemoveAt(i);
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Dark midnight cosmic background
        buffer.Fill(4, 3, 10);

        // 1. Render Tail Nodes (Violet -> Cyan Comet Tail)
        for (int i = 0; i < _trail.Count; i++)
        {
            var node = _trail[i];
            double progress = (double)i / Math.Max(1, _trail.Count);
            byte alpha = (byte)(progress * 180);

            var key = KeyMapTable.FindNearest(node.X, node.Y);
            if (key != null)
            {
                byte r = (byte)(140 * progress);
                byte g = (byte)(40 + (progress * 180));
                byte b = 255;
                buffer.BlendPixel(key.Offset, r, g, b, alpha);
            }
        }

        // 2. Render Main Cursor Orb (Blazing Cyan/White Core)
        var headKey = KeyMapTable.FindNearest(_curX, _curY);
        if (headKey != null)
        {
            buffer.SetColor(headKey.Offset, 200, 250, 255);
        }

        // Radial diffuse halo around cursor
        foreach (var key in KeyMapTable.AllKeys)
        {
            double kx = key.X + (key.Width * 0.5);
            double ky = key.Y + (key.Height * 0.5);
            double dist = Math.Sqrt(Math.Pow(kx - _curX, 2) + Math.Pow(ky - _curY, 2));

            if (dist > 0.4 && dist < 2.5)
            {
                double falloff = 1.0 - (dist / 2.5);
                byte alpha = (byte)(falloff * 140);
                buffer.BlendPixel(key.Offset, 124, 58, 237, alpha);
            }
        }
    }
}
