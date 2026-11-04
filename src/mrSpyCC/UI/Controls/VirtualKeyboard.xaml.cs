using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.UI.Controls;

public partial class VirtualKeyboard : UserControl
{
    private const double UNIT_SIZE = 42.0;
    private const double KEY_SPACING = 3.5;
    private const double PADDING = 8.0;

    private static readonly SolidColorBrush KeycapSurfaceBrush = CreateFrozenBrush(14, 17, 26);
    private static readonly SolidColorBrush HoverBorderBrush = CreateFrozenBrush(168, 85, 247);
    private static readonly SolidColorBrush SelectedBorderBrush = CreateFrozenBrush(0, 240, 255);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private readonly Dictionary<int, KeyVisualItem> _keyVisuals = new();
    private KeyVisualItem[] _visualArray = [];
    private int _selectedOffset = -1;
    private long _lastUiUpdateTicks;
    private int _uiUpdateQueued;
    private FrameBuffer? _latestBuffer;
    private readonly Action _renderUiAction;

    public event Action<KeyEntry>? KeyClicked;

    public int SelectedOffset
    {
        get => _selectedOffset;
        set
        {
            _selectedOffset = value;
            UpdateSelectionVisuals();
        }
    }

    public VirtualKeyboard()
    {
        InitializeComponent();
        _renderUiAction = RenderLatestBufferOnUiThread;
        BuildKeyboardVisuals();
    }

    private void BuildKeyboardVisuals()
    {
        KeyboardCanvas.Children.Clear();
        _keyVisuals.Clear();

        foreach (var key in KeyMapTable.AllKeys)
        {
            // Skip offset 321 standalone rendering: it is the bottom LED of the unified ISO Enter
            if (key.Offset == 321) continue;

            if (key.Offset == 349)
            {
                var isoContainer = CreateIsoEnterKey(key, out var isoVisual);
                KeyboardCanvas.Children.Add(isoContainer);
                _keyVisuals[349] = isoVisual;
                continue;
            }

            double left = PADDING + (key.X * (UNIT_SIZE + KEY_SPACING));
            double top = PADDING + (key.Y * (UNIT_SIZE + KEY_SPACING));
            double width = (key.Width * UNIT_SIZE) + ((key.Width - 1.0) * KEY_SPACING);
            double height = (key.Height * UNIT_SIZE) + ((key.Height - 1.0) * KEY_SPACING);

            // Reusable brushes per key
            var ledBrush = new SolidColorBrush(Color.FromRgb(20, 25, 40));
            var underglowBrush = new SolidColorBrush(Color.FromArgb(40, 124, 58, 237));
            var borderGlowBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

            var outerContainer = new Grid
            {
                Width = width,
                Height = height,
                Cursor = Cursors.Hand,
                ToolTip = $"{key.Name} (Offset: {key.Offset})\n{key.DisplayLabel}"
            };

            // 1. Underglow Diffuse Backplate Border
            var underglow = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = underglowBrush,
                Opacity = 0.55
            };
            outerContainer.Children.Add(underglow);

            // 2. Chiclet Keycap Surface
            var keycap = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = KeycapSurfaceBrush,
                BorderBrush = borderGlowBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1)
            };

            // 3. LED Diffuse Tint Overlay
            var ledTint = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = ledBrush,
                Opacity = 0.38
            };

            // 4. Key Label TextBlock
            var text = new TextBlock
            {
                Text = string.IsNullOrEmpty(key.DisplayLabel) ? key.Name : key.DisplayLabel,
                Foreground = Brushes.White,
                FontSize = key.DisplayLabel.Length > 4 ? 9.0 : (key.DisplayLabel.Length > 2 ? 10.0 : 11.5),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var innerGrid = new Grid();
            innerGrid.Children.Add(ledTint);
            innerGrid.Children.Add(text);
            keycap.Child = innerGrid;
            outerContainer.Children.Add(keycap);

            Canvas.SetLeft(outerContainer, left);
            Canvas.SetTop(outerContainer, top);

            int offset = key.Offset;
            outerContainer.MouseLeftButtonDown += (s, e) =>
            {
                SelectedOffset = offset;
                KeyClicked?.Invoke(key);
            };

            outerContainer.MouseEnter += (s, e) =>
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    SelectedOffset = offset;
                    KeyClicked?.Invoke(key);
                }

                if (offset != _selectedOffset)
                {
                    keycap.BorderBrush = HoverBorderBrush; // Violet hover
                    keycap.BorderThickness = new Thickness(1.5);
                }
            };

            outerContainer.MouseLeave += (s, e) =>
            {
                if (offset != _selectedOffset)
                {
                    keycap.BorderBrush = borderGlowBrush;
                    keycap.BorderThickness = new Thickness(1);
                }
            };

            KeyboardCanvas.Children.Add(outerContainer);
            _keyVisuals[offset] = new KeyVisualItem(offset, keycap, ledBrush, underglowBrush, borderGlowBrush, text, key);
        }

        _visualArray = [.. _keyVisuals.Values];
    }

    private UIElement CreateIsoEnterKey(KeyEntry key, out KeyVisualItem visualItem)
    {
        double dx = UNIT_SIZE + KEY_SPACING;
        double dy = UNIT_SIZE + KEY_SPACING;

        // Row 2 starts at X=13.5
        double x2_left = PADDING + (13.5 * dx);
        // Row 3 starts at X=12.75 (wider section next to 'İ')
        double x3_left = PADDING + (12.75 * dx);
        // Right edge of Enter aligns with KeyMapTable
        double x_right = PADDING + (13.5 * dx) + (1.6 * UNIT_SIZE) + (0.6 * KEY_SPACING);

        double y_top = PADDING + (2.0 * dy);
        double y_step = PADDING + (3.0 * dy);
        double y_bottom = PADDING + (3.0 * dy) + UNIT_SIZE;

        var streamGeom = new StreamGeometry();
        using (var ctx = streamGeom.Open())
        {
            ctx.BeginFigure(new Point(x2_left, y_top), true, true);
            ctx.LineTo(new Point(x_right, y_top), true, false);
            ctx.LineTo(new Point(x_right, y_bottom), true, false);
            ctx.LineTo(new Point(x3_left, y_bottom), true, false);
            ctx.LineTo(new Point(x3_left, y_step), true, false);
            ctx.LineTo(new Point(x2_left, y_step), true, false);
        }
        streamGeom.Freeze();

        var ledBrush = new SolidColorBrush(Color.FromRgb(20, 25, 40));
        var underglowBrush = new SolidColorBrush(Color.FromArgb(40, 124, 58, 237));
        var borderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

        var keycapPath = new System.Windows.Shapes.Path
        {
            Data = streamGeom,
            Fill = KeycapSurfaceBrush,
            Stroke = borderBrush,
            StrokeThickness = 1,
            Cursor = Cursors.Hand,
            ToolTip = "Enter (ISO Çift Katlı)\nOffset: 349 / 321"
        };

        var ledPath = new System.Windows.Shapes.Path
        {
            Data = streamGeom,
            Fill = ledBrush,
            Opacity = 0.38,
            IsHitTestVisible = false
        };

        var text = new TextBlock
        {
            Text = "Enter ⏎",
            Foreground = Brushes.White,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            IsHitTestVisible = false
        };

        var container = new Canvas { IsHitTestVisible = true };
        container.Children.Add(keycapPath);
        container.Children.Add(ledPath);
        container.Children.Add(text);

        Canvas.SetLeft(text, x3_left + 16);
        Canvas.SetTop(text, y_step + 12);

        int offset = key.Offset;
        keycapPath.MouseLeftButtonDown += (s, e) =>
        {
            SelectedOffset = offset;
            KeyClicked?.Invoke(key);
        };

        keycapPath.MouseEnter += (s, e) =>
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                SelectedOffset = offset;
                KeyClicked?.Invoke(key);
            }

            if (offset != _selectedOffset)
            {
                keycapPath.Stroke = HoverBorderBrush;
                keycapPath.StrokeThickness = 1.5;
            }
        };

        keycapPath.MouseLeave += (s, e) =>
        {
            if (offset != _selectedOffset)
            {
                keycapPath.Stroke = borderBrush;
                keycapPath.StrokeThickness = 1;
            }
        };

        visualItem = new KeyVisualItem(offset, null, ledBrush, underglowBrush, borderBrush, text, key, keycapPath);
        return container;
    }

    public void UpdateColors(FrameBuffer buffer)
    {
        // Sistem tepsisindeyken veya görünür değilken WPF UI iş parçacığını ve GC'yi sıfır yükte tut
        if (!IsVisible)
            return;

        long nowTicks = Environment.TickCount64;
        if (nowTicks - _lastUiUpdateTicks < 30)
            return;

        _lastUiUpdateTicks = nowTicks;
        _latestBuffer = buffer;

        if (Interlocked.CompareExchange(ref _uiUpdateQueued, 1, 0) == 0)
        {
            Dispatcher.InvokeAsync(_renderUiAction, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void RenderLatestBufferOnUiThread()
    {
        Interlocked.Exchange(ref _uiUpdateQueued, 0);
        var buffer = _latestBuffer;
        if (buffer == null || !IsVisible)
            return;

        var items = _visualArray;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var rgb = buffer.GetKey(item.Offset);

            // Delta kontrolü: Tuş rengi önceki kareyle birebir aynıysa WPF DependencyProperty tetikleme!
            if (item.HasRendered && item.LastColor == rgb)
                continue;

            item.LastColor = rgb;
            item.HasRendered = true;

            item.LedBrush.Color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
            item.UnderglowBrush.Color = Color.FromArgb(
                (byte)Math.Clamp((rgb.R + rgb.G + rgb.B) / 3, 20, 200),
                rgb.R, rgb.G, rgb.B);

            if (item.Offset != _selectedOffset)
            {
                item.BorderBrush.Color = Color.FromArgb(100, rgb.R, rgb.G, rgb.B);
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var item in _visualArray)
        {
            bool isSelected = (item.Offset == _selectedOffset);
            if (item.Keycap != null)
            {
                item.Keycap.BorderBrush = isSelected ? SelectedBorderBrush : item.BorderBrush;
                item.Keycap.BorderThickness = new Thickness(isSelected ? 2 : 1);
            }
            else if (item.IsoPath != null)
            {
                item.IsoPath.Stroke = isSelected ? SelectedBorderBrush : item.BorderBrush;
                item.IsoPath.StrokeThickness = isSelected ? 2 : 1;
            }
        }
    }

    private sealed class KeyVisualItem(
        int offset,
        Border? keycap,
        SolidColorBrush ledBrush,
        SolidColorBrush underglowBrush,
        SolidColorBrush borderBrush,
        TextBlock label,
        KeyEntry key,
        System.Windows.Shapes.Path? isoPath = null)
    {
        public int Offset { get; } = offset;
        public Border? Keycap { get; } = keycap;
        public SolidColorBrush LedBrush { get; } = ledBrush;
        public SolidColorBrush UnderglowBrush { get; } = underglowBrush;
        public SolidColorBrush BorderBrush { get; } = borderBrush;
        public TextBlock Label { get; } = label;
        public KeyEntry Key { get; } = key;
        public System.Windows.Shapes.Path? IsoPath { get; } = isoPath;
        public ColorRgb LastColor { get; set; }
        public bool HasRendered { get; set; }
    }
}
