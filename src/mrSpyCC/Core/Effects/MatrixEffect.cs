using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class MatrixEffect : EffectBase
{
    public override string Id => "matrix";
    public override string DisplayName => "Matrix (Terminal Code Rain)";

    public enum MatrixStyle
    {
        VerticalRain,   // Stil 0: Klasik dikey kod yağmuru
        AngledStreaks,  // Stil 1: Açılı siber çizgiler
        CrossLasers,    // Stil 2: Çapraz siber tarama lazerleri
        CyberRipples    // Stil 3: Sayısal dalga halkaları
    }

    public enum MatrixColorTheme
    {
        EmeraldGreen, // Klasik Zümrüt Yeşili
        AmberGold,    // Kehribar Altın
        GhostCyan,    // Hayalet Camgöbeği
        NeoViolet     // Siber Mor
    }

    public MatrixStyle Style { get; set; } = MatrixStyle.VerticalRain;
    public MatrixColorTheme ColorTheme { get; set; } = MatrixColorTheme.EmeraldGreen;
    public double Speed { get; set; } = 4.0;
    public double Density { get; set; } = 0.45;

    // Streaks for Style 0 (Vertical)
    private readonly struct RainColumn(double headY, double speed, double trailLength)
    {
        public double HeadY { get; } = headY;
        public double Speed { get; } = speed;
        public double TrailLength { get; } = trailLength;
    }

    private readonly RainColumn[] _columns = new RainColumn[24];
    private readonly Random _rnd = new();
    private double _time;

    // Streaks for Style 1 (Angled)
    private readonly double[] _streakAngle = new double[6];
    private readonly double[] _streakPos = new double[6];
    private readonly double[] _streakSpeed = new double[6];

    // Lasers for Style 2
    private readonly double[] _laserPos = new double[3];
    private readonly double[] _laserSpeed = new double[3];
    private readonly int[] _laserAxis = new int[3]; // 0=Horizontal, 1=Vertical

    // Ripples for Style 3
    private readonly double[] _rippleX = new double[3];
    private readonly double[] _rippleY = new double[3];
    private readonly double[] _rippleRadius = new double[3];

    public override void Reset()
    {
        _time = 0.0;
        for (int i = 0; i < _columns.Length; i++)
        {
            _columns[i] = new RainColumn(-(_rnd.NextDouble() * 8.0), (3.0 + _rnd.NextDouble() * 3.5), (3.0 + _rnd.NextDouble() * 2.5));
        }

        for (int i = 0; i < 6; i++)
        {
            _streakAngle[i] = _rnd.Next(30, 75) * Math.PI / 180.0;
            _streakPos[i] = -25.0 - (i * 8.0);
            _streakSpeed[i] = 12.0 + _rnd.NextDouble() * 15.0;
        }

        for (int i = 0; i < 3; i++)
        {
            _laserPos[i] = -15.0 - (i * 10.0);
            _laserSpeed[i] = 10.0 + _rnd.NextDouble() * 8.0;
            _laserAxis[i] = i % 2;
        }

        for (int i = 0; i < 3; i++)
        {
            _rippleX[i] = 4.0 + _rnd.NextDouble() * 14.0;
            _rippleY[i] = 1.0 + _rnd.NextDouble() * 4.0;
            _rippleRadius[i] = i * 6.0;
        }
    }

    public override void Update(double dt)
    {
        _time += dt;
        double speedMul = Speed * 0.8;

        switch (Style)
        {
            case MatrixStyle.VerticalRain:
                for (int i = 0; i < _columns.Length; i++)
                {
                    var col = _columns[i];
                    double ny = col.HeadY + (col.Speed * speedMul * dt);
                    if (ny - col.TrailLength > 7.0)
                    {
                        // Reset column
                        _columns[i] = new RainColumn(-(_rnd.NextDouble() * 5.0), (3.0 + _rnd.NextDouble() * 4.0), (2.5 + _rnd.NextDouble() * 3.0));
                    }
                    else
                    {
                        _columns[i] = new RainColumn(ny, col.Speed, col.TrailLength);
                    }
                }
                break;

            case MatrixStyle.AngledStreaks:
                for (int i = 0; i < 6; i++)
                {
                    _streakPos[i] += _streakSpeed[i] * speedMul * dt;
                    if (_streakPos[i] > 35.0)
                    {
                        _streakPos[i] = -30.0;
                        _streakAngle[i] = _rnd.Next(25, 75) * Math.PI / 180.0;
                        _streakSpeed[i] = 10.0 + _rnd.NextDouble() * 15.0;
                    }
                }
                break;

            case MatrixStyle.CrossLasers:
                for (int i = 0; i < 3; i++)
                {
                    _laserPos[i] += _laserSpeed[i] * speedMul * dt;
                    double limit = _laserAxis[i] == 0 ? 25.0 : 8.0;
                    if (_laserPos[i] > limit + 5.0)
                    {
                        _laserPos[i] = -8.0;
                        _laserAxis[i] = _rnd.Next(0, 2);
                        _laserSpeed[i] = 8.0 + _rnd.NextDouble() * 10.0;
                    }
                }
                break;

            case MatrixStyle.CyberRipples:
                for (int i = 0; i < 3; i++)
                {
                    _rippleRadius[i] += 8.0 * speedMul * dt;
                    if (_rippleRadius[i] > 22.0)
                    {
                        _rippleRadius[i] = 0.0;
                        _rippleX[i] = 3.0 + _rnd.NextDouble() * 16.0;
                        _rippleY[i] = 1.0 + _rnd.NextDouble() * 4.5;
                    }
                }
                break;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        buffer.Clear();
        ColorRgb baseColor = GetThemeColor();

        switch (Style)
        {
            case MatrixStyle.VerticalRain:
                RenderVerticalRain(buffer, baseColor);
                break;
            case MatrixStyle.AngledStreaks:
                RenderAngledStreaks(buffer, baseColor);
                break;
            case MatrixStyle.CrossLasers:
                RenderCrossLasers(buffer, baseColor);
                break;
            case MatrixStyle.CyberRipples:
                RenderCyberRipples(buffer, baseColor);
                break;
        }
    }

    private void RenderVerticalRain(FrameBuffer buffer, ColorRgb baseColor)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            int colIdx = Math.Clamp((int)key.X, 0, _columns.Length - 1);
            var col = _columns[colIdx];

            double distFromHead = col.HeadY - key.Y;
            if (distFromHead >= -0.2 && distFromHead <= col.TrailLength)
            {
                if (distFromHead < 0.45)
                {
                    // Bright white glyph head
                    buffer.SetKey(key.Offset, ColorRgb.White);
                }
                else
                {
                    // Fading phosphor trail
                    double trail = 1.0 - (distFromHead / col.TrailLength);
                    buffer.SetKey(key.Offset, baseColor.Scale(Math.Pow(trail, 1.4)));
                }
            }
        }
    }

    private void RenderAngledStreaks(FrameBuffer buffer, ColorRgb baseColor)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            double bestIntensity = 0.0;

            for (int s = 0; s < 6; s++)
            {
                double dirX = Math.Cos(_streakAngle[s]);
                double dirY = Math.Sin(_streakAngle[s]);
                double proj = key.X * dirX + key.Y * dirY;
                double dist = Math.Abs(proj - _streakPos[s]);

                if (dist < 2.0)
                {
                    double inten = 1.0 - (dist / 2.0);
                    if (inten > bestIntensity) bestIntensity = inten;
                }
            }

            if (bestIntensity > 0.01)
            {
                ColorRgb col = (bestIntensity > 0.85)
                    ? ColorRgb.Lerp(baseColor, ColorRgb.White, (bestIntensity - 0.85) / 0.15)
                    : baseColor.Scale(bestIntensity);
                buffer.SetKey(key.Offset, col);
            }
        }
    }

    private void RenderCrossLasers(FrameBuffer buffer, ColorRgb baseColor)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            double bestIntensity = 0.0;

            for (int i = 0; i < 3; i++)
            {
                double coord = _laserAxis[i] == 0 ? key.X : key.Y;
                double dist = Math.Abs(coord - _laserPos[i]);

                if (dist < 1.2)
                {
                    double inten = 1.0 - (dist / 1.2);
                    if (inten > bestIntensity) bestIntensity = inten;
                }
            }

            if (bestIntensity > 0.01)
            {
                ColorRgb col = (bestIntensity > 0.8)
                    ? ColorRgb.Lerp(baseColor, ColorRgb.White, (bestIntensity - 0.8) / 0.2)
                    : baseColor.Scale(bestIntensity);
                buffer.SetKey(key.Offset, col);
            }
        }
    }

    private void RenderCyberRipples(FrameBuffer buffer, ColorRgb baseColor)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            double bestIntensity = 0.0;

            for (int i = 0; i < 3; i++)
            {
                double dx = key.X - _rippleX[i];
                double dy = (key.Y - _rippleY[i]) * 1.8;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double ringDist = Math.Abs(dist - _rippleRadius[i]);

                if (ringDist < 1.4)
                {
                    double fade = Math.Max(0.0, 1.0 - (_rippleRadius[i] / 22.0));
                    double inten = (1.4 - ringDist) / 1.4 * fade;
                    if (inten > bestIntensity) bestIntensity = inten;
                }
            }

            if (bestIntensity > 0.01)
            {
                buffer.SetKey(key.Offset, baseColor.Scale(bestIntensity));
            }
        }
    }

    private ColorRgb GetThemeColor()
    {
        return ColorTheme switch
        {
            MatrixColorTheme.AmberGold => new ColorRgb(255, 175, 10),
            MatrixColorTheme.GhostCyan => new ColorRgb(0, 240, 255),
            MatrixColorTheme.NeoViolet => new ColorRgb(168, 85, 247),
            _ => new ColorRgb(0, 255, 60) // Classic Emerald Green
        };
    }
}
