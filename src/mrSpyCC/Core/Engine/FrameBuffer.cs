using System;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Engine;

public sealed class FrameBuffer
{
    public const int SIZE = 512;
    private readonly byte[] _bytes = new byte[SIZE];

    public ReadOnlySpan<byte> Span => _bytes;

    public void Clear()
    {
        Array.Clear(_bytes, 0, SIZE);
    }

    public void Fill(ColorRgb color)
    {
        for (int i = 0; i + 2 < SIZE; i += 3)
        {
            _bytes[i] = color.R;
            _bytes[i + 1] = color.G;
            _bytes[i + 2] = color.B;
        }
    }

    public void Fill(byte r, byte g, byte b) => Fill(new ColorRgb(r, g, b));

    public void SetKey(int offset, ColorRgb color)
    {
        if (offset >= 0 && offset + 2 < SIZE)
        {
            _bytes[offset] = color.R;
            _bytes[offset + 1] = color.G;
            _bytes[offset + 2] = color.B;

            // Fiziksel klavyedeki Çift LED'li ISO Enter tuşunun (349 = Alt/Ana, 321 = Üst)
            // her zaman yekpare olarak aynı renkte yanmasını garanti et.
            if (offset == KeyMapTable.IsoEnterPrimaryOffset)
            {
                const int sec = KeyMapTable.IsoEnterSecondaryOffset;
                _bytes[sec] = color.R;
                _bytes[sec + 1] = color.G;
                _bytes[sec + 2] = color.B;
            }
            else if (offset == KeyMapTable.IsoEnterSecondaryOffset)
            {
                const int pri = KeyMapTable.IsoEnterPrimaryOffset;
                _bytes[pri] = color.R;
                _bytes[pri + 1] = color.G;
                _bytes[pri + 2] = color.B;
            }
        }
    }

    public void SetKey(int offset, byte r, byte g, byte b) => SetKey(offset, new ColorRgb(r, g, b));
    public void SetColor(int offset, byte r, byte g, byte b) => SetKey(offset, new ColorRgb(r, g, b));
    public void SetColor(int offset, ColorRgb color) => SetKey(offset, color);

    public ColorRgb GetKey(int offset)
    {
        if (offset >= 0 && offset + 2 < SIZE)
        {
            return new ColorRgb(_bytes[offset], _bytes[offset + 1], _bytes[offset + 2]);
        }
        return ColorRgb.Black;
    }

    public void BlendKey(int offset, ColorRgb color, double alpha)
    {
        if (offset >= 0 && offset + 2 < SIZE)
        {
            var current = new ColorRgb(_bytes[offset], _bytes[offset + 1], _bytes[offset + 2]);
            var blended = current.Blend(color, alpha);
            _bytes[offset] = blended.R;
            _bytes[offset + 1] = blended.G;
            _bytes[offset + 2] = blended.B;

            if (offset == KeyMapTable.IsoEnterPrimaryOffset)
            {
                const int sec = KeyMapTable.IsoEnterSecondaryOffset;
                _bytes[sec] = blended.R;
                _bytes[sec + 1] = blended.G;
                _bytes[sec + 2] = blended.B;
            }
            else if (offset == KeyMapTable.IsoEnterSecondaryOffset)
            {
                const int pri = KeyMapTable.IsoEnterPrimaryOffset;
                _bytes[pri] = blended.R;
                _bytes[pri + 1] = blended.G;
                _bytes[pri + 2] = blended.B;
            }
        }
    }

    public void BlendKey(int offset, byte r, byte g, byte b, double alpha) => BlendKey(offset, new ColorRgb(r, g, b), alpha);
    public void BlendPixel(int offset, byte r, byte g, byte b, byte alpha) => BlendKey(offset, new ColorRgb(r, g, b), alpha / 255.0);

    public void CopyTo(byte[] destination)
    {
        Array.Copy(_bytes, destination, SIZE);
    }
}
