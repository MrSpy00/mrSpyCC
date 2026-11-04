using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace mrSpyCC.Core.Audio;

public sealed class WasapiAudioCapture : IDisposable
{
    private const int FFT_SIZE = 1024;
    private const int BANDS_COUNT = 16;

    private readonly float[] _sampleBuffer = new float[FFT_SIZE];
    private readonly float[] _real = new float[FFT_SIZE];
    private readonly float[] _imag = new float[FFT_SIZE];
    private readonly float[] _bands = new float[BANDS_COUNT];
    private readonly float[] _peakBands = new float[BANDS_COUNT];
    private readonly object _lock = new();

    private Thread? _captureThread;
    private volatile bool _isRunning;
    private int _sampleIndex;

    public volatile bool IsActiveConsumer;
    public float BassLevel { get; private set; }

    public void GetFrequencyBands(Span<float> destination)
    {
        lock (_lock)
        {
            _bands.AsSpan(0, Math.Min(_bands.Length, destination.Length)).CopyTo(destination);
        }
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _captureThread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "mrSpyCC_WasapiCaptureThread"
        };
        _captureThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _captureThread?.Join(500);
        _captureThread = null;
    }

    private void CaptureLoop()
    {
        IntPtr pEnumerator = IntPtr.Zero;
        IntPtr pDevice = IntPtr.Zero;
        IntPtr pAudioClient = IntPtr.Zero;
        IntPtr pCaptureClient = IntPtr.Zero;
        IntPtr pWaveFormat = IntPtr.Zero;

        try
        {
            var clsidEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            var iidEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

            int hr = CoCreateInstance(ref clsidEnumerator, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, ref iidEnumerator, out pEnumerator);
            if (hr < 0 || pEnumerator == IntPtr.Zero)
            {
                SimulatedAudioLoop();
                return;
            }

            // eRender = 0, eMultimedia = 1
            var enumerator = (IMMDeviceEnumerator)Marshal.GetObjectForIUnknown(pEnumerator);
            hr = enumerator.GetDefaultAudioEndpoint(0, 1, out pDevice);
            if (hr < 0 || pDevice == IntPtr.Zero)
            {
                SimulatedAudioLoop();
                return;
            }

            var device = (IMMDevice)Marshal.GetObjectForIUnknown(pDevice);
            var iidAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
            hr = device.Activate(ref iidAudioClient, 1, IntPtr.Zero, out pAudioClient);
            if (hr < 0 || pAudioClient == IntPtr.Zero)
            {
                SimulatedAudioLoop();
                return;
            }

            var audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(pAudioClient);
            hr = audioClient.GetMixFormat(out pWaveFormat);
            if (hr < 0 || pWaveFormat == IntPtr.Zero)
            {
                SimulatedAudioLoop();
                return;
            }

            var waveFormat = Marshal.PtrToStructure<WAVEFORMATEX>(pWaveFormat);

            // AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000
            const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
            const long BUFFER_DURATION_100NS = 10_000_000; // 1 second

            hr = audioClient.Initialize(
                0 /* AUDCLNT_SHAREMODE_SHARED */,
                AUDCLNT_STREAMFLAGS_LOOPBACK,
                BUFFER_DURATION_100NS,
                0,
                pWaveFormat,
                IntPtr.Zero);

            if (hr < 0)
            {
                SimulatedAudioLoop();
                return;
            }

            var iidCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C9E7594D8");
            hr = audioClient.GetService(ref iidCaptureClient, out pCaptureClient);
            if (hr < 0 || pCaptureClient == IntPtr.Zero)
            {
                SimulatedAudioLoop();
                return;
            }

            var captureClient = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCaptureClient);
            bool streamStarted = false;

            while (_isRunning)
            {
                if (!IsActiveConsumer)
                {
                    if (streamStarted)
                    {
                        try { audioClient.Stop(); } catch { }
                        streamStarted = false;
                        DecaySilence();
                    }
                    Thread.Sleep(150);
                    continue;
                }

                if (!streamStarted)
                {
                    try
                    {
                        audioClient.Start();
                        streamStarted = true;
                    }
                    catch { }
                }

                Thread.Sleep(12);
                bool receivedAudio = false;

                while (captureClient.GetNextPacketSize(out uint packetLength) >= 0 && packetLength > 0)
                {
                    hr = captureClient.GetBuffer(out IntPtr pData, out uint numFramesRead, out int dwFlags, out _, out _);
                    if (hr >= 0 && numFramesRead > 0)
                    {
                        // AUDCLNT_BUFFERFLAGS_SILENT = 0x2
                        if ((dwFlags & 0x2) == 0 && pData != IntPtr.Zero)
                        {
                            ProcessAudioSamples(pData, numFramesRead, waveFormat);
                            receivedAudio = true;
                        }
                        captureClient.ReleaseBuffer(numFramesRead);
                    }
                    else
                    {
                        break;
                    }
                }

                if (!receivedAudio)
                {
                    DecaySilence();
                }
            }

            if (streamStarted)
            {
                try { audioClient.Stop(); } catch { }
            }
        }
        catch
        {
            // Fallback gracefully
            SimulatedAudioLoop();
        }
        finally
        {
            if (pWaveFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(pWaveFormat);
            if (pCaptureClient != IntPtr.Zero) Marshal.Release(pCaptureClient);
            if (pAudioClient != IntPtr.Zero) Marshal.Release(pAudioClient);
            if (pDevice != IntPtr.Zero) Marshal.Release(pDevice);
            if (pEnumerator != IntPtr.Zero) Marshal.Release(pEnumerator);
        }
    }

    private void DecaySilence()
    {
        lock (_lock)
        {
            for (int b = 0; b < BANDS_COUNT; b++)
            {
                _bands[b] *= 0.85f;
                if (_bands[b] < 0.003f) _bands[b] = 0f;
            }
            BassLevel = (_bands[0] + _bands[1] + _bands[2]) / 3f;
        }
    }

    private unsafe void ProcessAudioSamples(IntPtr pData, uint numFrames, WAVEFORMATEX format)
    {
        if (format.wFormatTag != 3 && format.wFormatTag != 0xFFFE) // IEEE_FLOAT or EXTENSIBLE
            return;

        float* pSamples = (float*)pData;
        int channels = format.nChannels;

        for (uint i = 0; i < numFrames; i++)
        {
            float mono = 0;
            for (int c = 0; c < channels; c++)
            {
                mono += *pSamples++;
            }
            mono /= channels;

            _sampleBuffer[_sampleIndex++] = mono;
            if (_sampleIndex >= FFT_SIZE)
            {
                _sampleIndex = 0;
                ComputeFft();
            }
        }
    }

    private void ComputeFft()
    {
        _sampleBuffer.AsSpan().CopyTo(_real);
        Array.Clear(_imag, 0, FFT_SIZE);

        FastFourierTransform.ApplyHannWindow(_real);
        FastFourierTransform.Fft(_real, _imag);

        lock (_lock)
        {
            int binStep = (FFT_SIZE / 2) / BANDS_COUNT;
            for (int b = 0; b < BANDS_COUNT; b++)
            {
                float sum = 0;
                int start = b * binStep;
                for (int i = 0; i < binStep; i++)
                {
                    float r = _real[start + i];
                    float im = _imag[start + i];
                    sum += MathF.Sqrt(r * r + im * im);
                }
                float val = (sum / binStep) * 0.1f;
                val = Math.Clamp(val, 0f, 1f);

                // Smooth falloff
                if (val > _bands[b])
                    _bands[b] = val;
                else
                    _bands[b] = _bands[b] * 0.85f;
            }

            BassLevel = (_bands[0] + _bands[1] + _bands[2]) / 3f;
        }
    }

    private void SimulatedAudioLoop()
    {
        var rnd = new Random();
        while (_isRunning)
        {
            Thread.Sleep(30);
            lock (_lock)
            {
                for (int b = 0; b < BANDS_COUNT; b++)
                {
                    _bands[b] = Math.Clamp(_bands[b] * 0.8f + (float)rnd.NextDouble() * 0.2f, 0f, 1f);
                }
                BassLevel = _bands[0];
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    #region COM Interop Definitions
    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
    private static extern int CoCreateInstance(
        ref Guid clsid,
        IntPtr pUnkOuter,
        int dwClsContext,
        ref Guid iid,
        out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
        int GetBufferSize(out uint pNumBufferFrames);
        int GetStreamLatency(out long phnsLatency);
        int GetCurrentPadding(out uint pNumPaddingFrames);
        [PreserveSig]
        int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        [PreserveSig]
        int GetMixFormat(out IntPtr ppDeviceFormat);
        int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr eventHandle);
        [PreserveSig]
        int GetService(ref Guid iid, out IntPtr ppv);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C9E7594D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out int pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
        [PreserveSig]
        int ReleaseBuffer(uint numFramesRead);
        [PreserveSig]
        int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }
    #endregion
}
