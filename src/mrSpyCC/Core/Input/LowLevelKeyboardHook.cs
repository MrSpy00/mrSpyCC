using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Input;

public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const uint LLKHF_EXTENDED = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    private const uint WM_QUIT = 0x0012;

    private HookProc? _hookDelegate;
    private IntPtr _hookId = IntPtr.Zero;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private readonly ManualResetEventSlim _startedEvent = new(false);

    public event Action<KeyEntry, bool>? KeyActivity;

    public void Start()
    {
        if (_hookThread != null)
            return;

        _startedEvent.Reset();
        _hookThread = new Thread(HookThreadLoop)
        {
            IsBackground = true,
            Name = "mrSpyCC_KeyboardHookThread"
        };
        _hookThread.Start();
        _startedEvent.Wait(2000);
    }

    public void Stop()
    {
        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThread?.Join(1000);
            _hookThread = null;
            _hookThreadId = 0;
        }
    }

    private void HookThreadLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        _hookDelegate = HookCallback;

        var hMod = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookDelegate, hMod, 0);
        _startedEvent.Set();

        if (_hookId != IntPtr.Zero)
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (isDown || isUp)
            {
                int vk = Marshal.ReadInt32(lParam, 0);
                int flags = Marshal.ReadInt32(lParam, 8);
                bool isExtended = (flags & (int)LLKHF_EXTENDED) != 0;

                var entry = KeyMapTable.FindByVirtualKey(vk, isExtended);
                if (entry != null)
                {
                    try
                    {
                        KeyActivity?.Invoke(entry, isDown);
                    }
                    catch
                    {
                        // Protect hook thread from subscriber exceptions
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public void Dispose()
    {
        Stop();
        _startedEvent.Dispose();
    }
}
