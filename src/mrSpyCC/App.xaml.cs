using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Win32.SafeHandles;
using mrSpyCC.Core.VendorLock;
using mrSpyCC.UI.Tray;

namespace mrSpyCC;

public partial class App : Application
{
    private const string MUTEX_NAME = "mrSpyCC_SingleInstanceMutex_aegisSoft";
    private const string WAKEUP_EVENT_NAME = "mrSpyCC_WakeUpEvent_aegisSoft";

    private const int HWND_BROADCAST = 0xFFFF;
    private const uint ASFW_ANY = 0xFFFFFFFF;
    private const uint EVENT_MODIFY_STATE = 0x0002;
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint SDDL_REVISION_1 = 1;

    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _wakeUpEvent;
    private Thread? _wakeUpThread;
    private volatile bool _isExiting;

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string StringSecurityDescriptor,
        uint StringSDRevision,
        out IntPtr SecurityDescriptor,
        out uint SecurityDescriptorSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(
        ref SECURITY_ATTRIBUTES lpEventAttributes,
        bool bManualReset,
        bool bInitialState,
        string lpName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenEventW(
        uint dwDesiredAccess,
        bool bInheritHandle,
        string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), args.ExceptionObject?.ToString());
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_dispatcher.log"), args.Exception?.ToString());
            }
            catch { }
        };

        try
        {
            SetCurrentProcessExplicitAppUserModelID("aegisSoft.mrSpyCC.PRO.v1");
        }
        catch { }

        bool startMinimized = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase);

        // 1. Eğer --startup ile açıldıysa ve kullanıcı Görev Yöneticisi > Başlangıç Uygulamaları'ndan
        // mrSpyCC'yi devre dışı bıraktıysa, kullanıcının tercihine saygı duy ve sessizce çık.
        if (startMinimized && !TaskSchedulerHelper.IsStartupApprovedByWindows())
        {
            Shutdown();
            return;
        }

        // 2. Eğer HKCU\...\Run üzerinden standart yetkiyle (Medium Integrity) --startup çalıştıysa
        // ve Görev Zamanlayıcıda yüksek yetkili (Admin) görev mevcutsa, önce yüksek yetkili görevi tetikle.
        if (startMinimized && !TaskSchedulerHelper.IsRunningAsAdministrator() && !IsAnotherInstanceRunning())
        {
            if (TaskSchedulerHelper.IsScheduledTaskInstalled() && TaskSchedulerHelper.TryRunScheduledTask())
            {
                // Yüksek yetkili Görev Zamanlayıcı sürecinin başlaması için kısa süre bekle
                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(100);
                    if (IsAnotherInstanceRunning())
                    {
                        Shutdown();
                        return;
                    }
                }
            }
        }

        // 3. Tekil Örnek (Single-Instance) Kontrolü
        bool isNewInstance = TryAcquireSingleInstanceMutex();

        if (!isNewInstance)
        {
            // Eğer --startup ile ikinci bir kopya tetiklendiyse (örn: hem Run hem TaskScheduler),
            // pencereyi ekrana AÇMADAN sessizce çık!
            // Yalnızca kullanıcı mrSpyCC.exe'ye tıkladığında (!startMinimized) mevcut örneği uyandır ve öne getir.
            if (!startMinimized)
            {
                SignalExistingInstanceToOpen();
            }
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        StartWakeUpListener(mainWindow);

        // If launched via Task Scheduler / Run with --startup, remain in system tray (TrayIconManager EnsureHandle ile tepsi ikonu aktiftir)
        if (!startMinimized)
        {
            mainWindow.Show();
        }
    }

    private bool TryAcquireSingleInstanceMutex()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, MUTEX_NAME, out bool createdNew);
            _ownsMutex = createdNew;
            return createdNew;
        }
        catch (UnauthorizedAccessException)
        {
            // Yüksek yetkili (Admin) bir örnek zaten Mutex'i tutuyor demektir!
            _ownsMutex = false;
            return false;
        }
        catch
        {
            return !IsAnotherInstanceRunning();
        }
    }

    private static bool IsAnotherInstanceRunning()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(current.ProcessName);
            bool found = false;
            foreach (var p in processes)
            {
                if (p.Id != current.Id)
                {
                    found = true;
                }
                p.Dispose();
            }
            return found;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tüm yetki seviyelerinden (Low/Medium/High Integrity) sinyal alabilen EventWaitHandle dinleyicisini başlatır.
    /// </summary>
    private void StartWakeUpListener(MainWindow mainWindow)
    {
        IntPtr pSd = IntPtr.Zero;
        try
        {
            // D:(A;;GA;;;WD) -> Everyone (World) Generic All
            // S:(ML;;NW;;;LW) -> Low Mandatory Integrity Label (UIPI / MIC engelini tamamen kaldırır)
            const string sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
            SECURITY_ATTRIBUTES sa = new()
            {
                nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                bInheritHandle = 0
            };

            if (ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, SDDL_REVISION_1, out pSd, out _))
            {
                sa.lpSecurityDescriptor = pSd;
            }

            IntPtr rawHandle = CreateEventW(ref sa, bManualReset: false, bInitialState: false, WAKEUP_EVENT_NAME);
            if (rawHandle != IntPtr.Zero)
            {
                _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
                _wakeUpEvent.SafeWaitHandle.Close();
                _wakeUpEvent.SafeWaitHandle = new SafeWaitHandle(rawHandle, ownsHandle: true);
            }
            else
            {
                _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WAKEUP_EVENT_NAME);
            }
        }
        catch
        {
            try
            {
                _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WAKEUP_EVENT_NAME);
            }
            catch { }
        }
        finally
        {
            if (pSd != IntPtr.Zero)
            {
                LocalFree(pSd);
            }
        }

        if (_wakeUpEvent == null) return;

        _wakeUpThread = new Thread(() =>
        {
            while (!_isExiting)
            {
                try
                {
                    if (_wakeUpEvent.WaitOne(1000))
                    {
                        if (_isExiting) break;
                        mainWindow.Dispatcher.BeginInvoke(new Action(mainWindow.RestoreAndActivate));
                    }
                }
                catch
                {
                    if (_isExiting) break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "mrSpyCC_SingleInstanceWakeUpListener"
        };
        _wakeUpThread.Start();
    }

    /// <summary>
    /// Arka planda veya sistem tepsisinde çalışan mevcut mrSpyCC örneğine
    /// hem Kernel Event hem de Win32 Broadcast mesajı göndererek penceresini ekrana açtırır.
    /// </summary>
    private static void SignalExistingInstanceToOpen()
    {
        try
        {
            // Arka plandaki örneğin pencereyi en öne (Foreground) getirmesine izin ver
            AllowSetForegroundWindow(ASFW_ANY);
        }
        catch { }

        // Kanal 1: Cross-Integrity Kernel Event Sinyali
        try
        {
            IntPtr hEvent = OpenEventW(EVENT_MODIFY_STATE | SYNCHRONIZE, false, WAKEUP_EVENT_NAME);
            if (hEvent != IntPtr.Zero)
            {
                SetEvent(hEvent);
                CloseHandle(hEvent);
            }
            else if (EventWaitHandle.TryOpenExisting(WAKEUP_EVENT_NAME, out var evt))
            {
                evt.Set();
                evt.Dispose();
            }
        }
        catch { }

        // Kanal 2: UIPI-izinli Kayıtlı Pencere Mesajı Yayını (Broadcast)
        try
        {
            if (TrayIconManager.WM_SHOW_MRSPYCC != 0)
            {
                PostMessage((IntPtr)HWND_BROADCAST, TrayIconManager.WM_SHOW_MRSPYCC, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;
        try
        {
            _wakeUpEvent?.Set();
            _wakeUpEvent?.Dispose();
        }
        catch { }

        try
        {
            if (_ownsMutex && _singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            _singleInstanceMutex?.Dispose();
        }
        catch { }

        base.OnExit(e);
    }
}
