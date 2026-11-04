using System;

namespace mrSpyCC.Core.Engine;

public readonly record struct ColorRgb(byte R, byte G, byte B)
{
    public static readonly ColorRgb Black = new(0, 0, 0);
    public static readonly ColorRgb White = new(255, 255, 255);
    public static readonly ColorRgb Red = new(255, 0, 0);
    public static readonly ColorRgb Green = new(0, 255, 0);
    public static readonly ColorRgb Blue = new(0, 0, 255);
    public static readonly ColorRgb Cyan = new(0, 240, 255);
    public static readonly ColorRgb Magenta = new(255, 0, 200);
    public static readonly ColorRgb Yellow = new(255, 230, 0);
    public static readonly ColorRgb Orange = new(255, 120, 0);
    public static readonly ColorRgb Purple = new(160, 32, 240);

    public ColorRgb Scale(double factor)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);
        return new ColorRgb(
            (byte)(R * factor),
            (byte)(G * factor),
            (byte)(B * factor));
    }

    public ColorRgb Blend(ColorRgb over, double alpha)
    {
        alpha = Math.Clamp(alpha, 0.0, 1.0);
        double inv = 1.0 - alpha;
        return new ColorRgb(
            (byte)Math.Clamp(R * inv + over.R * alpha, 0, 255),
            (byte)Math.Clamp(G * inv + over.G * alpha, 0, 255),
            (byte)Math.Clamp(B * inv + over.B * alpha, 0, 255));
    }

    public static ColorRgb Lerp(ColorRgb a, ColorRgb b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return new ColorRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    public static ColorRgb FromHsv(double hueDegrees, double saturation = 1.0, double value = 1.0)
    {
        hueDegrees = (hueDegrees % 360.0 + 360.0) % 360.0;
        saturation = Math.Clamp(saturation, 0.0, 1.0);
        value = Math.Clamp(value, 0.0, 1.0);

        double c = value * saturation;
        double x = c * (1.0 - Math.Abs((hueDegrees / 60.0) % 2.0 - 1.0));
        double m = value - c;

        double r1, g1, b1;
        if (hueDegrees < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (hueDegrees < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (hueDegrees < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (hueDegrees < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (hueDegrees < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return new ColorRgb(
            (byte)Math.Clamp((r1 + m) * 255.0, 0, 255),
            (byte)Math.Clamp((g1 + m) * 255.0, 0, 255),
            (byte)Math.Clamp((b1 + m) * 255.0, 0, 255));
    }

    public static ColorRgb FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Black;
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new ColorRgb(r, g, b);
        }
        return Black;
    }

    public void ToHsv(out double h, out double s, out double v)
    {
        double r = R / 255.0;
        double g = G / 255.0;
        double b = B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max <= 0.0001 ? 0.0 : delta / max;

        if (delta <= 0.0001)
        {
            h = 0.0;
            return;
        }

        if (max == r)
        {
            h = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (max == g)
        {
            h = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            h = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (h < 0.0) h += 360.0;
    }

    public static ColorRgb SamplePalette(ReadOnlySpan<ColorRgb> palette, double t)
    {
        if (palette.Length == 0) return Black;
        if (palette.Length == 1) return palette[0];

        t = ((t % 1.0) + 1.0) % 1.0;
        double scaled = t * palette.Length;
        int idx0 = (int)scaled % palette.Length;
        int idx1 = (idx0 + 1) % palette.Length;
        double frac = scaled - Math.Floor(scaled);

        return Lerp(palette[idx0], palette[idx1], frac);
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}
