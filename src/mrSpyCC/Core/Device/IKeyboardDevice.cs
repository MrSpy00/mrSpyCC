using System;

namespace mrSpyCC.Core.Device;

public interface IKeyboardDevice : IDisposable
{
    bool IsConnected { get; }
    bool IsExclusiveLockHeld { get; }
    string? DevicePath { get; }
    int Brightness { get; set; }

    bool Connect(bool requireExclusive = true);
    void Disconnect();
    bool EnterCustomMode();
    bool SendFrame(ReadOnlySpan<byte> frameBuffer);
}
