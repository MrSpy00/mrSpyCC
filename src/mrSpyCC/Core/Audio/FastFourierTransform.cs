using System;

namespace mrSpyCC.Core.Audio;

public static class FastFourierTransform
{
    public static void Fft(Span<float> real, Span<float> imag)
    {
        int n = real.Length;
        if ((n & (n - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of 2");

        // Bit reversal
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        // Cooley-Tukey Radix-2
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            float wlenReal = (float)Math.Cos(angle);
            float wlenImag = (float)Math.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float wReal = 1.0f;
                float wImag = 0.0f;

                for (int k = 0; k < len / 2; k++)
                {
                    int uIdx = i + k;
                    int vIdx = i + k + len / 2;

                    float uReal = real[uIdx];
                    float uImag = imag[uIdx];
                    float vReal = real[vIdx] * wReal - imag[vIdx] * wImag;
                    float vImag = real[vIdx] * wImag + imag[vIdx] * wReal;

                    real[uIdx] = uReal + vReal;
                    imag[uIdx] = uImag + vImag;
                    real[vIdx] = uReal - vReal;
                    imag[vIdx] = uImag - vImag;

                    float nextWReal = wReal * wlenReal - wImag * wlenImag;
                    wImag = wReal * wlenImag + wImag * wlenReal;
                    wReal = nextWReal;
                }
            }
        }
    }

    public static void ApplyHannWindow(Span<float> buffer)
    {
        int n = buffer.Length;
        for (int i = 0; i < n; i++)
        {
            float multiplier = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (n - 1)));
            buffer[i] *= multiplier;
        }
    }
}
