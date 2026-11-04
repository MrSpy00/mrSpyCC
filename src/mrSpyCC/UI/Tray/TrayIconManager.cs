using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace mrSpyCC.UI.Tray;

/// <summary>
/// Windows Bildirim Alanı (System Tray) ve Görev Çubuğu İkon Yöneticisi.
/// aegisSoft kurumsal kimliği ve koyu siberpunk bağlam menüsü ile donatılmıştır.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 42;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint MSGFLT_ALLOW = 1;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    public static readonly uint WM_SHOW_MRSPYCC = RegisterWindowMessage("WM_AEGISSOFT_MRSPYCC_SHOW_WINDOW");
    private static readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>
    /// Uygulama arka plana (System Tray) alındığında veya ilk açılış tamamlandığında
    /// kullanılmayan JIT/WPF başlangıç bellek sayfalarını işletim sistemine iade eder.
    /// </summary>
    public static void TrimProcessMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);
            EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
        catch
        {
            // Ignore permission/OS handle errors
        }
    }

    private readonly Window _window;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _isInitialized;
    private bool _isCreated;
    private bool _isRetryRunning;
    private bool _isDisposed;
    private ContextMenu? _contextMenu;

    // Win32 ikon referansı çöp toplayıcı tarafından silinmemesi için nesne ömrü boyunca tutulur
    private System.Drawing.Icon? _iconHolder;

    public event Action? ToggleLightsRequested;
    public event Action? ExitRequested;

    public TrayIconManager(Window window)
    {
        _window = window;
        BuildContextMenu();

        // ÖNEMLİ: --startup ile arka planda başlarken _window.Show() çağrılmadığı için
        // WPF'in Loaded olayı tetiklenmez. EnsureHandle() ile HWND hemen oluşturulur
        // ve görev çubuğu (System Tray) ikonu anında görünür hale gelir.
        InitializeTray();
        _window.Loaded += (s, e) => InitializeTray();

        // Başlangıçtan 2 saniye sonra JIT/XAML önyükleme belleğini temizle
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(2000).ConfigureAwait(false);
            TrimProcessMemory();
        });
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(11, 13, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(6),
            HasDropShadow = true
        };

        // Modern Cyber MenuItem Stili (Beyaz çizgi ve boşlukları tamamen yok eder)
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "ItemBorder";
        borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 7, 12, 7));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 2, 2, 2));

        var textFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        textFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        textFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(textFactory);
        itemTemplate.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = MenuItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(124, 58, 237)), "ItemBorder"));
        itemTemplate.Triggers.Add(hoverTrigger);

        var titleItem = new MenuItem
        {
            Header = "⚡ mrSpyCC PRO — aegisSoft",
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252)),
            IsEnabled = false,
            Template = itemTemplate
        };

        var itemOpen = new MenuItem
        {
            Header = "🖥️  Kontrol Merkezini Aç",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = itemTemplate
        };
        itemOpen.Click += (s, e) => ShowMainWindow();

        var itemToggle = new MenuItem
        {
            Header = "💡  Işıkları Kapat / Aç",
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = itemTemplate
        };
        itemToggle.Click += (s, e) => ToggleLightsRequested?.Invoke();

        var itemLock = new MenuItem
        {
            Header = "🔒  Özel Donanım Kilidini Yenile",
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = itemTemplate
        };
        itemLock.Click += (s, e) =>
        {
            if (_window is MainWindow mw)
            {
                mw.RelockButton_Click(this, new RoutedEventArgs());
            }
        };

        var itemExit = new MenuItem
        {
            Header = "✕  Çıkış (mrSpyCC Kapat)",
            Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = itemTemplate
        };
        itemExit.Click += (s, e) => ExitRequested?.Invoke();

        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(34, 40, 60)), Margin = new Thickness(4, 3, 4, 3) });
        _contextMenu.Items.Add(itemOpen);
        _contextMenu.Items.Add(itemToggle);
        _contextMenu.Items.Add(itemLock);
        _contextMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(34, 40, 60)), Margin = new Thickness(4, 3, 4, 3) });
        _contextMenu.Items.Add(itemExit);
    }

    private void InitializeTray()
    {
        if (_isDisposed) return;

        if (!_isInitialized)
        {
            var interop = new WindowInteropHelper(_window);
            _hwnd = interop.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);

            // UIPI (User Interface Privilege Isolation) filtresi:
            // mrSpyCC Yönetici (Admin) olarak çalışırken standart yetkili explorer.exe veya
            // standart yetkili ikinci mrSpyCC.exe tıklamasından gelen mesajların engellenmesini önler.
            if (WM_SHOW_MRSPYCC != 0)
            {
                ChangeWindowMessageFilterEx(_hwnd, WM_SHOW_MRSPYCC, MSGFLT_ALLOW, IntPtr.Zero);
            }
            if (WM_TASKBARCREATED != 0)
            {
                ChangeWindowMessageFilterEx(_hwnd, WM_TASKBARCREATED, MSGFLT_ALLOW, IntPtr.Zero);
            }

            _isInitialized = true;
        }

        // 1. Primary: Extract associated icon from executable PE resources (100% self-contained portable guarantee)
        if (_iconHolder == null && !string.IsNullOrEmpty(Environment.ProcessPath) && System.IO.File.Exists(Environment.ProcessPath))
        {
            try
            {
                _iconHolder = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
            catch { }
        }

        // 2. Secondary: From resources/app.ico on disk
        if (_iconHolder == null)
        {
            try
            {
                string icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "resources", "app.ico");
                if (!System.IO.File.Exists(icoPath) && !string.IsNullOrEmpty(Environment.ProcessPath))
                {
                    string procDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? "";
                    icoPath = System.IO.Path.Combine(procDir, "resources", "app.ico");
                }

                if (System.IO.File.Exists(icoPath))
                {
                    using var fs = new System.IO.FileStream(icoPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    _iconHolder = new System.Drawing.Icon(fs, 32, 32);
                }
            }
            catch { }
        }

        // 3. Fallback: WPF embedded pack URI
        if (_iconHolder == null)
        {
            try
            {
                var uri = new Uri("pack://application:,,,/resources/app.ico");
                var sri = System.Windows.Application.GetResourceStream(uri);
                if (sri != null)
                {
                    using var stream = sri.Stream;
                    _iconHolder = new System.Drawing.Icon(stream, 32, 32);
                }
            }
            catch { }
        }

        RegisterOrUpdateNotifyIcon();

        // Erken Windows açılışında explorer.exe bildirim alanı henüz hazır değilse
        // arka planda hazır olana kadar yeniden dene.
        if (!_isCreated && !_isRetryRunning)
        {
            StartTrayRetryLoop();
        }
    }

    private void RegisterOrUpdateNotifyIcon()
    {
        if (_isDisposed || _hwnd == IntPtr.Zero) return;

        IntPtr hIcon = _iconHolder?.Handle ?? LoadIcon(IntPtr.Zero, (IntPtr)32512 /* IDI_APPLICATION */);

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = "mrSpyCC PRO — aegisSoft"
        };

        _isCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
        if (!_isCreated)
        {
            _isCreated = Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }
    }

    private async void StartTrayRetryLoop()
    {
        _isRetryRunning = true;
        try
        {
            for (int attempt = 0; attempt < 20 && !_isCreated && !_isDisposed; attempt++)
            {
                await System.Threading.Tasks.Task.Delay(1500);
                if (_isDisposed || _isCreated) break;

                _window.Dispatcher.Invoke(RegisterOrUpdateNotifyIcon);
            }
        }
        catch
        {
            // Ignore cancellation/shutdown exceptions
        }
        finally
        {
            _isRetryRunning = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == WM_LBUTTONDBLCLK || eventId == WM_LBUTTONUP)
            {
                ShowMainWindow();
                handled = true;
            }
            else if (eventId == WM_RBUTTONUP)
            {
                SetForegroundWindow(_hwnd);
                if (_contextMenu != null)
                {
                    _contextMenu.IsOpen = true;
                }
                handled = true;
            }
        }
        else if (WM_SHOW_MRSPYCC != 0 && (uint)msg == WM_SHOW_MRSPYCC)
        {
            // İkinci bir mrSpyCC.exe tıklandığında mevcut örneği uyandır ve öne getir
            ShowMainWindow();
            handled = true;
        }
        else if (WM_TASKBARCREATED != 0 && (uint)msg == WM_TASKBARCREATED)
        {
            // Explorer.exe yeniden başlatıldığında veya geç yüklendiğinde tepsi ikonunu yeniden kaydet
            _isCreated = false;
            RegisterOrUpdateNotifyIcon();
        }
        return IntPtr.Zero;
    }

    public void ShowMainWindow()
    {
        if (_isDisposed) return;

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            return;
        }

        // Eğer tepsi ikonu herhangi bir sebeple henüz oluşmamışsa onu da tazele
        if (!_isCreated)
        {
            RegisterOrUpdateNotifyIcon();
        }

        _window.ShowInTaskbar = true;
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        if (_hwnd != IntPtr.Zero)
        {
            ShowWindow(_hwnd, SW_SHOW);
            ShowWindow(_hwnd, SW_RESTORE);
            SetForegroundWindow(_hwnd);
        }

        // Windows odak çalma (Focus Stealing Prevention) kilidini aşmak için Topmost darbesi
        bool prevTopmost = _window.Topmost;
        _window.Topmost = true;
        _window.Activate();
        _window.Topmost = prevTopmost;
        _window.Focus();

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1200).ConfigureAwait(false);
            TrimProcessMemory();
        });
    }

    public void HideToTray()
    {
        _window.Hide();
        _window.ShowInTaskbar = false;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(400).ConfigureAwait(false);
            TrimProcessMemory();
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isCreated && _hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
        }

        _iconHolder?.Dispose();
        _iconHolder = null;

        _hwndSource?.RemoveHook(WndProc);
    }
}
