using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using mrSpyCC.Core.Effects;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;
using mrSpyCC.UI.Tray;
using mrSpyCC.UI.ViewModels;

namespace mrSpyCC;

public partial class MainWindow : Window
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLongW")]
    private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetClassLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;
    private const uint WM_SETICON = 0x0080;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private static readonly IntPtr ICON_SMALL = new IntPtr(0);
    private static readonly IntPtr ICON_BIG = new IntPtr(1);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private static readonly SolidColorBrush PaletteCardNormalBg = new(Color.FromRgb(18, 23, 38));
    private static readonly SolidColorBrush PaletteCardActiveBg = new(Color.FromRgb(34, 24, 58));
    private static readonly SolidColorBrush PaletteCardNormalBorder = new(Color.FromRgb(38, 49, 77));
    private static readonly SolidColorBrush PaletteCardActiveBorder = new(Color.FromRgb(0, 240, 255));
    private static readonly SolidColorBrush LockLockedBg = new(Color.FromRgb(16, 38, 28));
    private static readonly SolidColorBrush LockLockedBorder = new(Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush LockLockedFg = new(Color.FromRgb(74, 222, 128));
    private static readonly SolidColorBrush LockSharedBg = new(Color.FromRgb(42, 24, 14));
    private static readonly SolidColorBrush LockSharedBorder = new(Color.FromRgb(249, 115, 22));
    private static readonly SolidColorBrush LockSharedFg = new(Color.FromRgb(251, 146, 60));

    static MainWindow()
    {
        PaletteCardNormalBg.Freeze();
        PaletteCardActiveBg.Freeze();
        PaletteCardNormalBorder.Freeze();
        PaletteCardActiveBorder.Freeze();
        LockLockedBg.Freeze();
        LockLockedBorder.Freeze();
        LockLockedFg.Freeze();
        LockSharedBg.Freeze();
        LockSharedBorder.Freeze();
        LockSharedFg.Freeze();
    }

    private System.Drawing.Icon? _hIconHolder;
    private readonly List<Border> _studioPaletteCards = new(8);

    private readonly MainViewModel _vm;
    private readonly TrayIconManager _tray;
    private readonly DispatcherTimer _wpmTimer;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        _tray = new TrayIconManager(this);
        _tray.ToggleLightsRequested += OnToggleLights;
        _tray.ExitRequested += OnExitApp;

        // Route real-time rendered frame to the virtual keyboard UI visualizer
        _vm.Engine.FrameRendered += buffer => LiveKeyboard.UpdateColors(buffer);

        PopulateStudioPaletteCards();

        // Update Lock status visual pill
        UpdateLockPill();
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsExclusiveLocked) ||
                e.PropertyName == nameof(MainViewModel.LockStatusText))
            {
                UpdateLockPill();
            }
        };

        EffectsListBox.SelectionChanged += EffectsListBox_SelectionChanged;
        _vm.ProfileOrEffectStateRestored += SyncActiveEffectControlsUI;

        // Prevent horizontal ribbon snap-back bug when selecting category chips
        CategoryListBox.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((s, e) =>
        {
            e.Handled = true;
        }));

        CategoryListBox.PreviewMouseWheel += (s, e) =>
        {
            if (CategoryScrollViewer != null)
            {
                CategoryScrollViewer.ScrollToHorizontalOffset(CategoryScrollViewer.HorizontalOffset - (e.Delta * 0.75));
                e.Handled = true;
            }
        };

        // 1-second timer to update WPM display only when window is visible
        _wpmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wpmTimer.Tick += (s, e) =>
        {
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                _vm.UpdateWpm();
            }
        };
        _wpmTimer.Start();

        StaticZone3ColorPicker.TitleText = "3. Bölge (Sağ Numpad) Rengi";
        PerKeyColorPicker.TitleText = "Tuş Boyama Fırçası & Spektrum";

        ClampWindowToWorkArea();
        UpdateResponsiveLayout();
        SyncActiveEffectControlsUI();
    }

    private void PopulateStudioPaletteCards()
    {
        StudioPalettesGrid.Children.Clear();
        _studioPaletteCards.Clear();

        for (int i = 0; i < GlobalColorProcessor.StudioPalettes.Length; i++)
        {
            var pal = GlobalColorProcessor.GetPalette(i);

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int c = 0; c < pal.Colors.Length; c++)
            {
                var col = pal.Colors[c];
                grad.GradientStops.Add(new GradientStop(
                    Color.FromRgb(col.R, col.G, col.B),
                    c / (double)(pal.Colors.Length - 1)));
            }
            grad.Freeze();

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(new TextBlock
            {
                Text = pal.Name,
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 5)
            });
            stack.Children.Add(new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = grad
            });

            var cardBorder = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = PaletteCardNormalBg,
                BorderBrush = PaletteCardNormalBorder,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(8, 6, 8, 7),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = Cursors.Hand,
                Child = stack,
                Tag = i
            };

            int capturedIndex = i;
            cardBorder.MouseLeftButtonDown += (s, e) =>
            {
                _vm.ActiveStudioPaletteIndex = capturedIndex;
                _vm.ActiveColorMode = EffectColorMode.StudioPalette;
                SyncActiveEffectControlsUI();
                e.Handled = true;
            };

            _studioPaletteCards.Add(cardBorder);
            StudioPalettesGrid.Children.Add(cardBorder);
        }
    }

    public void RestoreAndActivate()
    {
        ClampWindowToWorkArea();
        _tray.ShowMainWindow();
        UpdateResponsiveLayout();
        SyncActiveEffectControlsUI();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void ClampWindowToWorkArea()
    {
        try
        {
            Rect workArea = SystemParameters.WorkArea;
            if (workArea.Width <= 0 || workArea.Height <= 0) return;

            double maxNormalW = Math.Max(MinWidth, workArea.Width - 16);
            double maxNormalH = Math.Max(MinHeight, workArea.Height - 16);

            if (Width > maxNormalW) Width = maxNormalW;
            if (Height > maxNormalH) Height = maxNormalH;

            if (WindowState == WindowState.Normal && !double.IsNaN(Left) && !double.IsNaN(Top))
            {
                if (Left + Width > workArea.Right) Left = Math.Max(workArea.Left, workArea.Right - Width);
                if (Top + Height > workArea.Bottom) Top = Math.Max(workArea.Top, workArea.Bottom - Height);
                if (Left < workArea.Left) Left = workArea.Left;
                if (Top < workArea.Top) Top = workArea.Top;
            }
        }
        catch { }
    }

    private void UpdateResponsiveLayout()
    {
        try
        {
            if (KeyboardPreviewBox == null) return;
            double referenceHeight = ActualHeight > 0 ? ActualHeight : Height;
            if (referenceHeight <= 0 || double.IsNaN(referenceHeight)) return;

            // Dynamically scale virtual keyboard height (~20% of window height, clamped between 136px and 210px)
            // so the lower TabControl always has generous vertical room on every resolution and DPI scaling.
            double targetKbHeight = Math.Clamp(Math.Round(referenceHeight * 0.20), 136.0, 210.0);
            if (Math.Abs(KeyboardPreviewBox.Height - targetKbHeight) > 0.5)
            {
                KeyboardPreviewBox.Height = targetKbHeight;
            }
        }
        catch { }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Prevent accidental mode changes or list jumps when pressing 'M' or any character key
        if (e.OriginalSource is not TextBox)
        {
            if (e.Key != Key.Tab && e.Key != Key.Escape)
            {
                if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    e.Handled = true;
                }
            }
        }
    }

    private Point _headerMouseDownPoint;
    private bool _isHeaderMouseDown;

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2)
        {
            _isHeaderMouseDown = false;
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        _isHeaderMouseDown = true;
        _headerMouseDownPoint = e.GetPosition(this);
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isHeaderMouseDown || e.LeftButton != MouseButtonState.Pressed) return;

        Point current = e.GetPosition(this);
        Vector diff = current - _headerMouseDownPoint;

        if (WindowState == WindowState.Maximized)
        {
            if (diff.Y > 35 || Math.Abs(diff.X) > 50)
            {
                _isHeaderMouseDown = false;
                double mouseRatioX = Math.Clamp(current.X / ActualWidth, 0.1, 0.9);
                var screenPoint = PointToScreen(current);
                WindowState = WindowState.Normal;
                MaximizeButton.Content = "🗖";
                Left = screenPoint.X - (Width * mouseRatioX);
                Top = Math.Max(0, screenPoint.Y - 15);
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
        else if (WindowState == WindowState.Normal)
        {
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                _isHeaderMouseDown = false;
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    private void Header_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isHeaderMouseDown = false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "🗗";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _tray.HideToTray();
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabControl) return;

        if (MainTabControl.SelectedIndex == 1)
        {
            _vm.ApplyEffectById("static_per_key");
        }
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string catName)
        {
            int idx = _vm.Categories.IndexOf(catName);
            if (idx >= 0)
            {
                _vm.SelectedCategoryIndex = idx;
            }
        }
    }

    private void UpdateLockPill()
    {
        if (_vm.IsExclusiveLocked)
        {
            LockStatusBorder.Background = LockLockedBg;
            LockStatusBorder.BorderBrush = LockLockedBorder;
            LockStatusTextBlock.Foreground = LockLockedFg;
            LockStatusTextBlock.Text = "✓ Özel Kilit Aktif";
            HeaderRelockBtn.Visibility = Visibility.Collapsed;

            SystemLockStatusTitle.Text = "✓ Özel Donanım Kilidi AKTİF";
            SystemLockStatusTitle.Foreground = LockLockedFg;
            SystemLockStatusDesc.Text = "Cihaz mrSpyCC tarafından güvenle kilitlenmiştir. Game Garaj yazılımı fanları ve güç modlarını yönetmeye devam ederken RGB çakışması tamamen engellenmiştir. Hiçbir işlem yapmanıza gerek yoktur.";
            SystemRelockBtn.Content = "Kilidi Doğrula / Yenile";
            SystemRelockBtn.Style = (Style)FindResource("CyberButton");
        }
        else
        {
            LockStatusBorder.Background = LockSharedBg;
            LockStatusBorder.BorderBrush = LockSharedBorder;
            LockStatusTextBlock.Foreground = LockSharedFg;
            LockStatusTextBlock.Text = "⚠️ Paylaşımlı Mod";
            HeaderRelockBtn.Visibility = Visibility.Visible;

            SystemLockStatusTitle.Text = "⚠️ Paylaşımlı Erişim Modu (Kilit Alınmadı)";
            SystemLockStatusTitle.Foreground = LockSharedFg;
            SystemLockStatusDesc.Text = "Cihaz paylaşımlı modda açık. Game Garaj Control Center arka planda ışıkları sıfırlayabilir. Kalıcı ve çakışmasız kontrol için 'Özel Kilidi Al' butonuna tıklayınız.";
            SystemRelockBtn.Content = "🔒 Özel Donanım Kilidini Al";
            SystemRelockBtn.Style = (Style)FindResource("PrimaryAccentButton");
        }
    }

    private void EffectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm.SelectedEffectItem == null) return;
        string id = _vm.SelectedEffectItem.Id;

        if (MainTabControl != null && (EffectsListBox.IsMouseOver || EffectsListBox.IsKeyboardFocusWithin))
        {
            if (id == "static_per_key")
            {
                MainTabControl.SelectedIndex = 1;
            }
            else if (MainTabControl.SelectedIndex >= 1)
            {
                MainTabControl.SelectedIndex = 0;
            }
        }

        StaticControlsPanel.Visibility = (id == "static") ? Visibility.Visible : Visibility.Collapsed;
        BreathingControlsPanel.Visibility = (id == "breathing") ? Visibility.Visible : Visibility.Collapsed;
        WaveControlsPanel.Visibility = (id == "wave") ? Visibility.Visible : Visibility.Collapsed;
        RainbowWaveControlsPanel.Visibility = (id == "rainbow_wave") ? Visibility.Visible : Visibility.Collapsed;
        ReactiveControlsPanel.Visibility = (id == "reactive") ? Visibility.Visible : Visibility.Collapsed;
        GamingControlsPanel.Visibility = (id == "gaming_mode") ? Visibility.Visible : Visibility.Collapsed;
        HeatmapControlsPanel.Visibility = (id == "typing_heatmap") ? Visibility.Visible : Visibility.Collapsed;
        VisualizerControlsPanel.Visibility = (id == "audio_visualizer") ? Visibility.Visible : Visibility.Collapsed;
        FlameControlsPanel.Visibility = (id == "flame") ? Visibility.Visible : Visibility.Collapsed;
        MatrixControlsPanel.Visibility = (id == "matrix") ? Visibility.Visible : Visibility.Collapsed;
        DigitalRainControlsPanel.Visibility = (id == "digital_rain") ? Visibility.Visible : Visibility.Collapsed;
        AuroraControlsPanel.Visibility = (id == "aurora") ? Visibility.Visible : Visibility.Collapsed;
        FireworksControlsPanel.Visibility = (id == "fireworks") ? Visibility.Visible : Visibility.Collapsed;
        PartyControlsPanel.Visibility = (id == "party") ? Visibility.Visible : Visibility.Collapsed;
        ScanControlsPanel.Visibility = (id == "scan") ? Visibility.Visible : Visibility.Collapsed;
        PulseControlsPanel.Visibility = (id == "pulse") ? Visibility.Visible : Visibility.Collapsed;
        StrobeControlsPanel.Visibility = (id == "strobe") ? Visibility.Visible : Visibility.Collapsed;

        bool hasCustomPanel = id is "static" or "breathing" or "wave" or "rainbow_wave" or "reactive" or "gaming_mode"
            or "typing_heatmap" or "audio_visualizer" or "flame" or "matrix" or "digital_rain"
            or "aurora" or "fireworks" or "party" or "scan" or "pulse" or "strobe";

        GenericControlsPanel.Visibility = hasCustomPanel ? Visibility.Collapsed : Visibility.Visible;
        ParametersScrollViewer?.ScrollToTop();
        SyncActiveEffectControlsUI();
    }

    private void SetActiveButton(Button? activeBtn, params Button[] group)
    {
        var accentStyle = (Style)FindResource("PrimaryAccentButton");
        var cyberStyle = (Style)FindResource("CyberButton");

        foreach (var btn in group)
        {
            if (btn != null)
            {
                btn.Style = (btn == activeBtn) ? accentStyle : cyberStyle;
            }
        }
    }

    private void SyncActiveEffectControlsUI()
    {
        try
        {
            string effId = _vm.SelectedEffectItem?.Id ?? _vm.ActiveEffect?.Id ?? "wave";

            // 0. Sync Global Color, Dual-Color, Palette & RGB Studio Card
            GlobalColorStudioCard.Visibility = (effId is "off" or "static_per_key")
                ? Visibility.Collapsed
                : Visibility.Visible;

            var colorMode = _vm.ActiveColorMode;
            Button activeModeBtn = colorMode switch
            {
                EffectColorMode.SingleColor => GlobalModeSingleBtn,
                EffectColorMode.DualColor => GlobalModeDualBtn,
                EffectColorMode.RainbowRgb => GlobalModeRgbBtn,
                EffectColorMode.StudioPalette => GlobalModePaletteBtn,
                _ => GlobalModeDefaultBtn
            };
            SetActiveButton(activeModeBtn, GlobalModeDefaultBtn, GlobalModeSingleBtn, GlobalModeDualBtn, GlobalModeRgbBtn, GlobalModePaletteBtn);

            bool isNativeCustomColorEffect = effId is "static" or "breathing" or "reactive" or "gaming_mode" or "scan" or "pulse" or "strobe";
            bool isNativeTwoColorEffect = effId == "gaming_mode" ||
                                          (effId == "static" && _vm.Static.Mode != StaticEffect.StaticLayoutMode.UniformColor);

            GlobalCustomColorPanel.Visibility = Visibility.Collapsed;
            GlobalRgbModePanel.Visibility = Visibility.Collapsed;
            GlobalStudioPalettePanel.Visibility = Visibility.Collapsed;

            switch (colorMode)
            {
                case EffectColorMode.Default:
                    if (isNativeCustomColorEffect)
                    {
                        GlobalCustomColorPanel.Visibility = Visibility.Visible;
                        if (isNativeTwoColorEffect)
                        {
                            DualColorToolbar.Visibility = Visibility.Visible;
                            DualBlendSelectorPanel.Visibility = Visibility.Collapsed;
                            Grid.SetColumnSpan(PrimaryPickerContainer, 1);
                            PrimaryPickerContainer.Margin = new Thickness(0, 0, 5, 0);
                            SecondaryPickerContainer.Visibility = Visibility.Visible;

                            if (effId == "gaming_mode")
                            {
                                GlobalPrimaryColorPicker.TitleText = "1. Ana Oyun Tuşları (WASD / QWER / Oklar)";
                                GlobalSecondaryColorPicker.TitleText = "2. Aksiyon & Yetenek Tuşları (Shift / 1-6)";
                            }
                            else if (_vm.Static.Mode == StaticEffect.StaticLayoutMode.ThreeZoneSplit)
                            {
                                GlobalPrimaryColorPicker.TitleText = "1. Bölge (Sol Klavye) Rengi";
                                GlobalSecondaryColorPicker.TitleText = "2. Bölge (Orta Klavye) Rengi";
                            }
                            else
                            {
                                GlobalPrimaryColorPicker.TitleText = "1. WASD & Ok Tuşları Vurgu Rengi";
                                GlobalSecondaryColorPicker.TitleText = "2. Çevre Tuşlar Rengi";
                            }
                        }
                        else
                        {
                            DualColorToolbar.Visibility = Visibility.Collapsed;
                            Grid.SetColumnSpan(PrimaryPickerContainer, 2);
                            PrimaryPickerContainer.Margin = new Thickness(0);
                            SecondaryPickerContainer.Visibility = Visibility.Collapsed;
                            GlobalPrimaryColorPicker.TitleText = "Efekt Ana Rengi & 360° Spektrum";
                        }
                        GlobalColorModeHintText.Text = "Efektin kendi renk düzeni aktif. Dilerseniz yukarıdan Çift Renk, Tam RGB veya Stüdyo Paletine geçebilirsiniz.";
                    }
                    else
                    {
                        GlobalColorModeHintText.Text = "Efekt orijinal renk temasında çalışıyor. Tek renk, 2-renk, Tam RGB veya Stüdyo Paleti için yukarıdan seçim yapın.";
                    }
                    break;

                case EffectColorMode.SingleColor:
                    GlobalCustomColorPanel.Visibility = Visibility.Visible;
                    DualColorToolbar.Visibility = Visibility.Collapsed;
                    Grid.SetColumnSpan(PrimaryPickerContainer, 2);
                    PrimaryPickerContainer.Margin = new Thickness(0);
                    SecondaryPickerContainer.Visibility = Visibility.Collapsed;
                    GlobalPrimaryColorPicker.TitleText = "Tek Özel Renk (Aktif Efektin Tüm Tonlarına Uygulanır)";
                    GlobalColorModeHintText.Text = "Tek Özel Renk Modu aktif: Efektin tüm animasyon ve ışık katmanları seçtiğiniz renkte çalışır.";
                    break;

                case EffectColorMode.DualColor:
                    GlobalCustomColorPanel.Visibility = Visibility.Visible;
                    DualColorToolbar.Visibility = Visibility.Visible;
                    DualBlendSelectorPanel.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(PrimaryPickerContainer, 1);
                    PrimaryPickerContainer.Margin = new Thickness(0, 0, 5, 0);
                    SecondaryPickerContainer.Visibility = Visibility.Visible;
                    GlobalPrimaryColorPicker.TitleText = "1. Ana Renk (Birincil Ton)";
                    GlobalSecondaryColorPicker.TitleText = "2. Yardımcı Renk (İkincil Ton)";
                    GlobalColorModeHintText.Text = "Çift Renk (2-Renk) Modu aktif: Efekt 1. ve 2. renkleriniz arasında seçtiğiniz geçiş stiliyle harmanlanır.";
                    break;

                case EffectColorMode.RainbowRgb:
                    GlobalRgbModePanel.Visibility = Visibility.Visible;
                    GlobalColorModeHintText.Text = "Tam RGB Gökkuşağı Modu aktif: Efektin hareket ve parlaklık fiziği korunarak 360° akıcı RGB spektrumuna dönüştürüldü.";
                    break;

                case EffectColorMode.StudioPalette:
                    GlobalStudioPalettePanel.Visibility = Visibility.Visible;
                    GlobalColorModeHintText.Text = $"Stüdyo Paleti aktif ({GlobalColorProcessor.GetPalette(_vm.ActiveStudioPaletteIndex).Name}): Efekt çok duraklı profesyonel renk paletiyle işleniyor.";
                    break;
            }

            GlobalPrimaryColorPicker.SetColorSilently(_vm.GlobalPrimaryColor);
            GlobalSecondaryColorPicker.SetColorSilently(_vm.GlobalSecondaryColor);
            StaticZone3ColorPicker.SetColorSilently(_vm.Static.Zone3Color);

            // Sync Dual Blend Mode Buttons
            var blendActive = _vm.ActiveDualBlendMode switch
            {
                DualColorBlendMode.HorizontalGradient => DualBlendHorizBtn,
                DualColorBlendMode.VerticalRadial => DualBlendVertBtn,
                DualColorBlendMode.AlternatingWave => DualBlendWaveBtn,
                _ => DualBlendToneBtn
            };
            SetActiveButton(blendActive, DualBlendToneBtn, DualBlendHorizBtn, DualBlendVertBtn, DualBlendWaveBtn);

            // Sync RGB Flow Style Buttons
            var rgbStyleActive = _vm.ActiveRgbStyle switch
            {
                RgbFlowStyle.SpectrumSync => RgbStyleSyncBtn,
                RgbFlowStyle.RadialPinwheel => RgbStyleSpiralBtn,
                _ => RgbStyleWaveBtn
            };
            SetActiveButton(rgbStyleActive, RgbStyleWaveBtn, RgbStyleSyncBtn, RgbStyleSpiralBtn);

            // Sync Studio Palette Cards
            int activePalIdx = _vm.ActiveStudioPaletteIndex;
            for (int i = 0; i < _studioPaletteCards.Count; i++)
            {
                var card = _studioPaletteCards[i];
                bool isSel = (i == activePalIdx);
                card.Background = isSel ? PaletteCardActiveBg : PaletteCardNormalBg;
                card.BorderBrush = isSel ? PaletteCardActiveBorder : PaletteCardNormalBorder;
                card.BorderThickness = isSel ? new Thickness(2.0) : new Thickness(1.5);
            }

            // 1. Static Mode
            switch (_vm.Static.Mode)
            {
                case StaticEffect.StaticLayoutMode.ThreeZoneSplit:
                    SetActiveButton(StaticModeThreeZoneBtn, StaticModeUniformBtn, StaticModeThreeZoneBtn, StaticModeWasdBtn);
                    StaticZoneExtraPanel.Visibility = Visibility.Visible;
                    StaticPrimaryColorLabel.Text = "Yukarıdaki 1. ve 2. Renk Seçiciden Sol ve Orta bölgeyi, aşağıdan 3. Bölge (Sağ Numpad) rengini seçebilirsiniz.";
                    break;
                case StaticEffect.StaticLayoutMode.WasdHighlight:
                    SetActiveButton(StaticModeWasdBtn, StaticModeUniformBtn, StaticModeThreeZoneBtn, StaticModeWasdBtn);
                    StaticZoneExtraPanel.Visibility = Visibility.Collapsed;
                    StaticPrimaryColorLabel.Text = "Yukarıdaki 1. Renk WASD & Okları, 2. Renk ise çevre tuşları aydınlatır.";
                    break;
                default:
                    SetActiveButton(StaticModeUniformBtn, StaticModeUniformBtn, StaticModeThreeZoneBtn, StaticModeWasdBtn);
                    StaticZoneExtraPanel.Visibility = Visibility.Collapsed;
                    StaticPrimaryColorLabel.Text = "Yukarıdaki Renk Stüdyosundan tüm klavye rengini veya Çift Renk / RGB modunu seçebilirsiniz.";
                    break;
            }

            // 2. Wave Palette & Direction
            var wavePalActive = _vm.Wave.PaletteIndex switch
            {
                1 => WavePal1Btn,
                2 => WavePal2Btn,
                3 => WavePal3Btn,
                _ => WavePal0Btn
            };
            SetActiveButton(wavePalActive, WavePal0Btn, WavePal1Btn, WavePal2Btn, WavePal3Btn);

            int waveDeg = (int)Math.Round(_vm.Wave.AngleDegrees);
            Button? waveDirActive = waveDeg switch
            {
                90 => WaveDir90Btn,
                270 => WaveDir270Btn,
                0 => WaveDir0Btn,
                45 => WaveDir45Btn,
                _ => null
            };
            SetActiveButton(waveDirActive, WaveDir90Btn, WaveDir270Btn, WaveDir0Btn, WaveDir45Btn);

            // 3. Reactive Mode
            var reactiveActive = _vm.Reactive.Mode switch
            {
                ReactiveEffect.ReactiveMode.SingleKey => ReactiveSingleBtn,
                ReactiveEffect.ReactiveMode.LaserBeam => ReactiveLaserBtn,
                _ => ReactiveRippleBtn
            };
            SetActiveButton(reactiveActive, ReactiveRippleBtn, ReactiveSingleBtn, ReactiveLaserBtn);

            // 4. Gaming Preset
            var gamingActive = _vm.GamingMode.Preset switch
            {
                GamingModeEffect.GamePreset.MobaStrategy => GamingMobaBtn,
                GamingModeEffect.GamePreset.ArrowKeysDriving => GamingArrowBtn,
                _ => GamingFpsBtn
            };
            SetActiveButton(gamingActive, GamingFpsBtn, GamingMobaBtn, GamingArrowBtn);

            // 5. Flame Type
            var flameActive = _vm.Flame.Type switch
            {
                FlameEffect.FlameType.IcePlasma => FlameIceBtn,
                FlameEffect.FlameType.ToxicDragon => FlameToxicBtn,
                _ => FlameHellfireBtn
            };
            SetActiveButton(flameActive, FlameHellfireBtn, FlameIceBtn, FlameToxicBtn);

            // 6. Matrix Style & Color Theme
            var matrixStyleActive = _vm.Matrix.Style switch
            {
                MatrixEffect.MatrixStyle.AngledStreaks => MatrixStyleAngledBtn,
                MatrixEffect.MatrixStyle.CrossLasers => MatrixStyleLaserBtn,
                MatrixEffect.MatrixStyle.CyberRipples => MatrixStyleRippleBtn,
                _ => MatrixStyleVertBtn
            };
            SetActiveButton(matrixStyleActive, MatrixStyleVertBtn, MatrixStyleAngledBtn, MatrixStyleLaserBtn, MatrixStyleRippleBtn);

            var matrixThemeActive = _vm.Matrix.ColorTheme switch
            {
                MatrixEffect.MatrixColorTheme.AmberGold => MatrixAmberBtn,
                MatrixEffect.MatrixColorTheme.GhostCyan => MatrixCyanBtn,
                MatrixEffect.MatrixColorTheme.NeoViolet => MatrixVioletBtn,
                _ => MatrixGreenBtn
            };
            SetActiveButton(matrixThemeActive, MatrixGreenBtn, MatrixAmberBtn, MatrixCyanBtn, MatrixVioletBtn);

            // 7. Digital Rain Palette
            var torrentActive = _vm.DigitalRain.Palette switch
            {
                DigitalRainEffect.TorrentPalette.SynthwavePurple => TorrentSynthBtn,
                DigitalRainEffect.TorrentPalette.ToxicGlitch => TorrentToxicBtn,
                _ => TorrentCyberBtn
            };
            SetActiveButton(torrentActive, TorrentCyberBtn, TorrentSynthBtn, TorrentToxicBtn);

            // 8. Party Mode
            var partyActive = _vm.Party.Mode switch
            {
                1 => PartyMode1Btn,
                2 => PartyMode2Btn,
                _ => PartyMode0Btn
            };
            SetActiveButton(partyActive, PartyMode0Btn, PartyMode1Btn, PartyMode2Btn);

            // 9. Generic / Interactive Control Panel Hint & Speed Visibility
            var currentEff = _vm.SelectedEffectItem?.Effect;
            if (GenericControlHintText != null)
            {
                GenericControlHintText.Text = _vm.GetEffectControlHint(currentEff);
            }
            if (GenericSpeedPanel != null)
            {
                GenericSpeedPanel.Visibility = _vm.SupportsGenericSpeed(currentEff) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch { }
    }

    private void GlobalColorMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int modeInt))
        {
            _vm.ActiveColorMode = (EffectColorMode)Math.Clamp(modeInt, 0, 4);
            SyncActiveEffectControlsUI();
        }
    }

    private void GlobalPrimaryColorPicker_ColorChanged(ColorRgb color)
    {
        _vm.GlobalPrimaryColor = color;
    }

    private void GlobalSecondaryColorPicker_ColorChanged(ColorRgb color)
    {
        _vm.GlobalSecondaryColor = color;
    }

    private void SwapDualColors_Click(object sender, RoutedEventArgs e)
    {
        _vm.SwapDualColors();
    }

    private void QuickDualCombo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr)
        {
            string[] parts = tagStr.Split('|');
            if (parts.Length == 2)
            {
                _vm.ApplyQuickDualPreset(ColorRgb.FromHex(parts[0]), ColorRgb.FromHex(parts[1]));
            }
        }
    }

    private void DualBlendMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int blendInt))
        {
            _vm.ActiveDualBlendMode = (DualColorBlendMode)Math.Clamp(blendInt, 0, 3);
            SyncActiveEffectControlsUI();
        }
    }

    private void RgbStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int styleInt))
        {
            _vm.ActiveRgbStyle = (RgbFlowStyle)Math.Clamp(styleInt, 0, 2);
            SyncActiveEffectControlsUI();
        }
    }

    private void ResetEffect_Click(object sender, RoutedEventArgs e)
    {
        _vm.ResetActiveEffect();
    }

    private void GenericSpeedPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            _vm.GenericSpeedMultiplier = val;
        }
    }

    private void BrightnessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
        {
            _vm.Brightness = val;
        }
    }

    private void StaticModeUniform_Click(object sender, RoutedEventArgs e)
    {
        _vm.Static.Mode = StaticEffect.StaticLayoutMode.UniformColor;
        if (_vm.ActiveColorMode == EffectColorMode.DualColor)
            _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void StaticModeThreeZone_Click(object sender, RoutedEventArgs e)
    {
        _vm.Static.Mode = StaticEffect.StaticLayoutMode.ThreeZoneSplit;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void StaticModeWasd_Click(object sender, RoutedEventArgs e)
    {
        _vm.Static.Mode = StaticEffect.StaticLayoutMode.WasdHighlight;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void StaticZone3ColorPicker_ColorChanged(ColorRgb color)
    {
        _vm.Static.Zone3Color = color;
        _vm.SaveCurrentState();
    }

    private void LiveKeyboard_KeyClicked(KeyEntry key)
    {
        if (LastClickedKeyText != null)
        {
            string label = !string.IsNullOrEmpty(key.DisplayLabel) ? key.DisplayLabel : key.Name;
            LastClickedKeyText.Text = $"{label} (Ofset: {key.Offset})";
        }

        if (MainTabControl.SelectedIndex == 1 || _vm.ActiveEffect?.Id == "static_per_key")
        {
            var color = PerKeyColorPicker.SelectedColor;
            _vm.StaticPainter.SetKeyColor(key.Offset, color);
            if (key.Offset == 349)
            {
                _vm.StaticPainter.SetKeyColor(321, color);
            }
            _vm.SaveCurrentState();
        }
        else
        {
            _vm.Engine.OnKeyActivity(key, true);
            Task.Delay(120).ContinueWith(_ => _vm.Engine.OnKeyActivity(key, false));
        }
    }

    private void PaintAll_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.ClearAll(color);
        _vm.SaveCurrentState();
    }

    private void PaintWasd_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.Wasd, color);
        _vm.SaveCurrentState();
    }

    private void PaintArrows_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.Arrows, color);
        _vm.SaveCurrentState();
    }

    private void PaintNumpad_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.Numpad, color);
        _vm.SaveCurrentState();
    }

    private void PaintFn_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.FunctionRow, color);
        _vm.SaveCurrentState();
    }

    private void PaintNumbers_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.Numbers, color);
        _vm.SaveCurrentState();
    }

    private void PaintAlpha_Click(object sender, RoutedEventArgs e)
    {
        var color = PerKeyColorPicker.SelectedColor;
        _vm.StaticPainter.SetGroupColor(KeyGroup.Alphanumeric, color);
        _vm.SaveCurrentState();
    }

    private void PaintBlack_Click(object sender, RoutedEventArgs e)
    {
        _vm.StaticPainter.ClearAll(ColorRgb.Black);
        _vm.SaveCurrentState();
    }

    private void WaveDir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double deg))
        {
            _vm.Wave.AngleDegrees = deg;
            _vm.SaveCurrentState();
            SyncActiveEffectControlsUI();
        }
    }

    private void FlameHellfire_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flame.Type = Core.Effects.FlameEffect.FlameType.Hellfire;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void FlameIce_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flame.Type = Core.Effects.FlameEffect.FlameType.IcePlasma;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void FlameToxic_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flame.Type = Core.Effects.FlameEffect.FlameType.ToxicDragon;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixStyleVertical_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.Style = Core.Effects.MatrixEffect.MatrixStyle.VerticalRain;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixStyleAngled_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.Style = Core.Effects.MatrixEffect.MatrixStyle.AngledStreaks;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixStyleLaser_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.Style = Core.Effects.MatrixEffect.MatrixStyle.CrossLasers;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixStyleRipple_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.Style = Core.Effects.MatrixEffect.MatrixStyle.CyberRipples;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixGreen_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.ColorTheme = Core.Effects.MatrixEffect.MatrixColorTheme.EmeraldGreen;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixAmber_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.ColorTheme = Core.Effects.MatrixEffect.MatrixColorTheme.AmberGold;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixCyan_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.ColorTheme = Core.Effects.MatrixEffect.MatrixColorTheme.GhostCyan;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MatrixViolet_Click(object sender, RoutedEventArgs e)
    {
        _vm.Matrix.ColorTheme = Core.Effects.MatrixEffect.MatrixColorTheme.NeoViolet;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void TorrentCyber_Click(object sender, RoutedEventArgs e)
    {
        _vm.DigitalRain.Palette = Core.Effects.DigitalRainEffect.TorrentPalette.CyberpunkNeon;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void TorrentSynth_Click(object sender, RoutedEventArgs e)
    {
        _vm.DigitalRain.Palette = Core.Effects.DigitalRainEffect.TorrentPalette.SynthwavePurple;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void TorrentToxic_Click(object sender, RoutedEventArgs e)
    {
        _vm.DigitalRain.Palette = Core.Effects.DigitalRainEffect.TorrentPalette.ToxicGlitch;
        _vm.ActiveColorMode = EffectColorMode.Default;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void PartyMode0_Click(object sender, RoutedEventArgs e)
    {
        _vm.Party.Mode = 0;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void PartyMode1_Click(object sender, RoutedEventArgs e)
    {
        _vm.Party.Mode = 1;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void PartyMode2_Click(object sender, RoutedEventArgs e)
    {
        _vm.Party.Mode = 2;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void RippleMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.Reactive.Mode = Core.Effects.ReactiveEffect.ReactiveMode.RippleSplash;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void SingleKeyMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.Reactive.Mode = Core.Effects.ReactiveEffect.ReactiveMode.SingleKey;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void LaserMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.Reactive.Mode = Core.Effects.ReactiveEffect.ReactiveMode.LaserBeam;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void FpsPreset_Click(object sender, RoutedEventArgs e)
    {
        _vm.GamingMode.Preset = Core.Effects.GamingModeEffect.GamePreset.FpsCompetitive;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void MobaPreset_Click(object sender, RoutedEventArgs e)
    {
        _vm.GamingMode.Preset = Core.Effects.GamingModeEffect.GamePreset.MobaStrategy;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    private void ArrowPreset_Click(object sender, RoutedEventArgs e)
    {
        _vm.GamingMode.Preset = Core.Effects.GamingModeEffect.GamePreset.ArrowKeysDriving;
        _vm.SaveCurrentState();
        SyncActiveEffectControlsUI();
    }

    public void RelockButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestExclusiveLock();
    }

    private void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string profileName)
        {
            _vm.ApplyProfile(profileName);
        }
    }

    private void CreateProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        NewProfileNameBox.Text = $"Özel Profil {_vm.Profiles.Count + 1}";
        NewProfileDescBox.Text = "";
        NewProfilePanel.Visibility = Visibility.Visible;
        NewProfileNameBox.Focus();
        NewProfileNameBox.SelectAll();
    }

    private void CancelNewProfile_Click(object sender, RoutedEventArgs e)
    {
        NewProfilePanel.Visibility = Visibility.Collapsed;
    }

    private void SaveNewProfile_Click(object sender, RoutedEventArgs e)
    {
        string name = NewProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        string? desc = string.IsNullOrWhiteSpace(NewProfileDescBox.Text) ? null : NewProfileDescBox.Text.Trim();

        _vm.CreateProfile(name, desc);
        NewProfilePanel.Visibility = Visibility.Collapsed;
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Core.Config.ProfileModel profile)
        {
            if (_vm.Profiles.Count <= 1)
            {
                MessageBox.Show("Sistemde en az bir profil bulunmalıdır. Son profil silinemez.", "mrSpyCC Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"'{profile.Name}' profilini silmek istediğinize emin misiniz?", "Profili Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _vm.DeleteProfile(profile);
            }
        }
    }

    private void ExportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Core.Config.ProfileModel profile)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Profil Dosyası (*.json)|*.json",
                FileName = $"mrSpyCC_{profile.Name.Replace(" ", "_")}.json",
                Title = "Profili JSON Olarak Dışa Aktar"
            };

            if (sfd.ShowDialog() == true)
            {
                if (_vm.ExportProfile(profile, sfd.FileName))
                {
                    MessageBox.Show("Profil başarıyla JSON dosyası olarak kaydedildi!", "mrSpyCC", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    private void ImportProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Profil Dosyası (*.json)|*.json",
            Title = "Profil Dosyasını Seç (.json)"
        };

        if (ofd.ShowDialog() == true)
        {
            if (_vm.ImportProfile(ofd.FileName))
            {
                MessageBox.Show("Profil başarıyla içe aktarıldı ve hemen uygulandı!", "mrSpyCC", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Profil dosyası yüklenemedi. Dosya biçimini kontrol ediniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OnToggleLights()
    {
        Dispatcher.Invoke(() =>
        {
            _vm.Brightness = _vm.Brightness > 0 ? 0 : 100;
        });
    }

    private void OnExitApp()
    {
        _wpmTimer.Stop();
        _tray.Dispose();
        _vm.Dispose();
        Application.Current.Shutdown();
    }

    private void WavePalette_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int pal))
        {
            _vm.Wave.PaletteIndex = pal;
            _vm.ActiveColorMode = EffectColorMode.Default;
            _vm.SaveCurrentState();
            SyncActiveEffectControlsUI();
        }
    }

    private void FavoriteStar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string effectId)
        {
            _vm.ToggleFavorite(effectId);
            e.Handled = true;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WindowProc);
            ClampWindowToWorkArea();
            UpdateResponsiveLayout();

            if (_hIconHolder == null && !string.IsNullOrEmpty(Environment.ProcessPath) && System.IO.File.Exists(Environment.ProcessPath))
            {
                try
                {
                    _hIconHolder = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                }
                catch { }
            }

            if (_hIconHolder == null)
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
                        _hIconHolder = new System.Drawing.Icon(fs);
                    }
                }
                catch { }
            }

            if (_hIconHolder == null)
            {
                try
                {
                    var sri = Application.GetResourceStream(new Uri("pack://application:,,,/resources/app.ico"));
                    if (sri != null)
                    {
                        using var stream = sri.Stream;
                        _hIconHolder = new System.Drawing.Icon(stream);
                    }
                }
                catch { }
            }

            if (_hIconHolder != null)
            {
                SendMessage(hwnd, WM_SETICON, ICON_SMALL, _hIconHolder.Handle);
                SendMessage(hwnd, WM_SETICON, ICON_BIG, _hIconHolder.Handle);

                SetClassLongPtr(hwnd, GCLP_HICON, _hIconHolder.Handle);
                SetClassLongPtr(hwnd, GCLP_HICONSM, _hIconHolder.Handle);
            }
        }
        catch { }
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        try
        {
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }
            }

            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            mmi.ptMinTrackSize.X = (int)Math.Round(MinWidth * dpiX);
            mmi.ptMinTrackSize.Y = (int)Math.Round(MinHeight * dpiY);

            Marshal.StructureToPtr(mmi, lParam, true);
        }
        catch { }
    }

    private void SettingsGearBtn_Click(object sender, RoutedEventArgs e)
    {
        SettingsModalOverlay.Visibility = (SettingsModalOverlay.Visibility == Visibility.Visible)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OpenGithub_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/MrSpy00");
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://aegissoft.com.tr");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        _tray.HideToTray();
    }
}
