using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace mrSpyCC.Core.Device;

public sealed class Ite8291Device : IKeyboardDevice
{
    public const ushort ITE_VID = 0x048D;
    public const ushort ITE_PID_600B = 0x600B;
    public const ushort VENDOR_USAGE_PAGE = 0xFF03;
    public const int BUFFER_SIZE = 512;

    private readonly object _lock = new();
    private SafeFileHandle? _handle;
    private string? _devicePath;
    private bool _isExclusive;
    private int _brightness = 100;

    // Preallocated buffers to eliminate GC allocations in the 30 FPS hot render loop
    private readonly byte[] _featureHeader = [0x00, 0x12, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00];
    private readonly byte[] _chunkBuffer = new byte[65];
    private readonly byte[] _scaledBuffer = new byte[BUFFER_SIZE];

    public bool IsConnected => _handle != null && !_handle.IsInvalid && !_handle.IsClosed;
    public bool IsExclusiveLockHeld => _isExclusive;
    public string? DevicePath => _devicePath;

    public int Brightness
    {
        get => _brightness;
        set => _brightness = Math.Clamp(value, 0, 100);
    }

    public bool Connect(bool requireExclusive = true)
    {
        lock (_lock)
        {
            Disconnect();

            var devices = NativeHid.EnumerateDevices(ITE_VID, 0);
            HidDeviceInfo? selected = null;
            int bestScore = 0;

            foreach (var d in devices)
            {
                int score = 0;
                if (d.UsagePage == VENDOR_USAGE_PAGE)
                    score = 4;
                else if (d.DevicePath.Contains("&mi_01", StringComparison.OrdinalIgnoreCase))
                    score = 3;

                if (score > 0 && d.ProductId == ITE_PID_600B)
                    score += 2;

                if (score > bestScore)
                {
                    bestScore = score;
                    selected = d;
                }
            }

            if (selected == null)
                return false;

            _devicePath = selected.Value.DevicePath;

            // Step A: Attempt exclusive lock (dwShareMode = 0)
            _handle = NativeHid.CreateFile(
                _devicePath,
                NativeHid.GENERIC_READ | NativeHid.GENERIC_WRITE,
                0, // EXCLUSIVE LOCK: no sharing with GCUBridge
                IntPtr.Zero,
                NativeHid.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (!_handle.IsInvalid)
            {
                _isExclusive = true;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                if (requireExclusive)
                {
                    _handle = null;
                    _isExclusive = false;
                    return false;
                }

                // Fallback to shared mode if requested
                _handle = NativeHid.CreateFile(
                    _devicePath,
                    NativeHid.GENERIC_READ | NativeHid.GENERIC_WRITE,
                    NativeHid.FILE_SHARE_READ | NativeHid.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeHid.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (_handle.IsInvalid)
                {
                    _handle = null;
                    _isExclusive = false;
                    return false;
                }

                _isExclusive = false;
            }

            return EnterCustomMode();
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            if (_handle != null)
            {
                if (!_handle.IsClosed && !_handle.IsInvalid)
                {
                    _handle.Dispose();
                }
                _handle = null;
            }
            _isExclusive = false;
        }
    }

    public bool EnterCustomMode()
    {
        lock (_lock)
        {
            if (!IsConnected)
                return false;

            // Step 1: EC Prep 1 (Buffer reset)
            byte[] prep1 = [0x00, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00];
            if (!NativeHid.HidD_SetFeature(_handle!, prep1, (uint)prep1.Length))
                return false;

            // Step 2: Clear hardware matrix frame
            byte[] clearFrame = new byte[65];
            NativeHid.WriteFile(_handle!, clearFrame, (uint)clearFrame.Length, out _, IntPtr.Zero);

            // Step 3: EC Prep 2 (Enable custom per-key mode 0x33, max brightness 0x32 = 50)
            byte[] prep2 = [0x00, 0x08, 0x02, 0x33, 0x00, 0x32, 0x00, 0x00, 0x00];
            if (!NativeHid.HidD_SetFeature(_handle!, prep2, (uint)prep2.Length))
                return false;

            return true;
        }
    }

    public bool SendFrame(ReadOnlySpan<byte> frameBuffer)
    {
        if (frameBuffer.Length < BUFFER_SIZE)
            return false;

        lock (_lock)
        {
            if (!IsConnected)
                return false;

            ReadOnlySpan<byte> source = frameBuffer;

            // Scale brightness if under 100%
            if (_brightness < 100)
            {
                int b = _brightness;
                for (int i = 0; i < BUFFER_SIZE; i++)
                {
                    _scaledBuffer[i] = (byte)((frameBuffer[i] * b) / 100);
                }
                source = _scaledBuffer;
            }

            // Step 1: Send Frame Header (Feature Report: 0x12, 8 chunks)
            if (!NativeHid.HidD_SetFeature(_handle!, _featureHeader, (uint)_featureHeader.Length))
            {
                // Attempt automatic reconnect if communication dropped
                if (!EnterCustomMode())
                    return false;
                if (!NativeHid.HidD_SetFeature(_handle!, _featureHeader, (uint)_featureHeader.Length))
                    return false;
            }

            // Step 2: Write 8 chunks of 64 bytes each (Total = 512 bytes)
            _chunkBuffer[0] = 0x00; // Report ID
            for (int chunkIndex = 0; chunkIndex < 8; chunkIndex++)
            {
                source.Slice(chunkIndex * 64, 64).CopyTo(_chunkBuffer.AsSpan(1, 64));

                if (!NativeHid.WriteFile(_handle!, _chunkBuffer, 65, out uint written, IntPtr.Zero) || written != 65)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
