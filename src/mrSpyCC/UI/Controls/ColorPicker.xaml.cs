using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using mrSpyCC.Core.Engine;

namespace mrSpyCC.UI.Controls;

public partial class ColorPicker : UserControl
{
    private static readonly SolidColorBrush SwatchNormalBorder = new(Color.FromArgb(85, 255, 255, 255));
    private static readonly SolidColorBrush SwatchActiveBorder = new(Color.FromRgb(0, 240, 255));

    static ColorPicker()
    {
        SwatchNormalBorder.Freeze();
        SwatchActiveBorder.Freeze();
    }

    private sealed class SwatchEntry
    {
        public required ColorRgb Color { get; init; }
        public required Border Border { get; init; }
    }

    private readonly List<SwatchEntry> _swatchEntries = new(16);
    private readonly SolidColorBrush _previewBrush = new(Color.FromRgb(124, 58, 237));
    private bool _isUpdatingInternally;
    private bool _isDraggingHue;
    private bool _isDraggingTone;

    private ColorRgb _selectedColor = new(124, 58, 237);
    private double _currentHue = 262.0;
    private double _currentTonePos = 0.5; // 0.0 = White Pastel, 0.5 = Pure Neon Hue, 1.0 = Deep Shade

    public event Action<ColorRgb>? ColorChanged;

    public string TitleText
    {
        get => HeaderTitleText.Text;
        set => HeaderTitleText.Text = value;
    }

    public ColorRgb SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            SyncHsvFromColor(value);
            UpdateUiFromColor();
            ColorChanged?.Invoke(_selectedColor);
        }
    }

    public void SetColorSilently(ColorRgb color)
    {
        _selectedColor = color;
        SyncHsvFromColor(color);
        UpdateUiFromColor();
    }

    public ColorPicker()
    {
        _isUpdatingInternally = true;
        InitializeComponent();
        ColorPreviewBorder.Background = _previewBrush;
        PopulateDefaultSwatches();
        _isUpdatingInternally = false;
        SetColorSilently(new ColorRgb(124, 58, 237));
    }

    private void PopulateDefaultSwatches()
    {
        (ColorRgb Col, string Label)[] swatches =
        [
            (new(124, 58, 237),  "aegisSoft Mor (#7C3AED)"),
            (new(0, 240, 255),   "Siber Camgöbeği (#00F0FF)"),
            (new(255, 0, 128),   "Neon Macenta (#FF0080)"),
            (new(0, 255, 128),   "Matrix Yeşili (#00FF80)"),
            (new(255, 180, 0),   "Siber Kehribar (#FFB400)"),
            (new(255, 30, 60),   "Kızıl Lazer (#FF1E3C)"),
            (new(59, 130, 246),  "Elektrik Mavisi (#3B82F6)"),
            (new(255, 95, 31),   "Volkanik Turuncu (#FF5F1F)"),
            (new(168, 85, 247),  "Ametist Lavanta (#A855F7)"),
            (new(20, 241, 149),  "Nane Turkuaz (#14F195)"),
            (new(255, 230, 60),  "Güneş Sarısı (#FFE63C)"),
            (new(236, 72, 153),  "Synthwave Pembe (#EC4899)"),
            (new(56, 189, 248),  "Buzul Mavisi (#38BDF8)"),
            (new(255, 255, 255), "Saf Beyaz (#FFFFFF)"),
            (new(99, 102, 241),  "Gece İndigo (#6366F1)"),
            (new(16, 20, 32),    "Gece Gizlilik / Koyu (#101420)")
        ];

        SwatchesPanel.Children.Clear();
        _swatchEntries.Clear();

        foreach (var (col, label) in swatches)
        {
            var brush = new SolidColorBrush(Color.FromRgb(col.R, col.G, col.B));
            brush.Freeze();

            var swatchBorder = new Border
            {
                Width = 21,
                Height = 21,
                CornerRadius = new CornerRadius(5),
                Background = brush,
                BorderBrush = SwatchNormalBorder,
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = Cursors.Hand,
                ToolTip = label
            };

            ColorRgb captured = col;
            swatchBorder.MouseLeftButtonDown += (s, e) =>
            {
                SelectedColor = captured;
                e.Handled = true;
            };

            _swatchEntries.Add(new SwatchEntry { Color = col, Border = swatchBorder });
            SwatchesPanel.Children.Add(swatchBorder);
        }
    }

    private void SyncHsvFromColor(ColorRgb color)
    {
        color.ToHsv(out double h, out double s, out double v);
        if (s > 0.04 && v > 0.04)
        {
            _currentHue = h;
        }

        // Map (s, v) onto our Tone bar [0..1]:
        // [0.0 .. 0.5]: White (s=0,v=1) -> Pure Hue (s=1,v=1)
        // [0.5 .. 1.0]: Pure Hue (s=1,v=1) -> Black (v=0)
        if (v >= 0.92 && s < 0.98)
        {
            _currentTonePos = 0.5 * s;
        }
        else
        {
            _currentTonePos = 0.5 + 0.5 * (1.0 - v);
        }
    }

    private void UpdateUiFromColor()
    {
        _isUpdatingInternally = true;
        try
        {
            SliderR.Value = _selectedColor.R;
            SliderG.Value = _selectedColor.G;
            SliderB.Value = _selectedColor.B;

            TextR.Text = _selectedColor.R.ToString();
            TextG.Text = _selectedColor.G.ToString();
            TextB.Text = _selectedColor.B.ToString();

            _previewBrush.Color = Color.FromRgb(_selectedColor.R, _selectedColor.G, _selectedColor.B);
            HexInputTextBox.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            RgbSummaryText.Text = $"RGB({_selectedColor.R}, {_selectedColor.G}, {_selectedColor.B})";

            ColorRgb pureHue = ColorRgb.FromHsv(_currentHue, 1.0, 1.0);
            TonePureHueStop.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);

            UpdateSpectrumThumbs();
            UpdateSwatchHighlights();
        }
        finally
        {
            _isUpdatingInternally = false;
        }
    }

    private void UpdateSpectrumThumbs()
    {
        double hueWidth = HueBarContainer.ActualWidth;
        if (hueWidth > 10)
        {
            double x = Math.Clamp(_currentHue / 360.0, 0.0, 1.0) * (hueWidth - HueThumb.Width);
            Canvas.SetLeft(HueThumb, x);
        }

        double toneWidth = ToneBarContainer.ActualWidth;
        if (toneWidth > 10)
        {
            double x = Math.Clamp(_currentTonePos, 0.0, 1.0) * (toneWidth - ToneThumb.Width);
            Canvas.SetLeft(ToneThumb, x);
        }
    }

    private void UpdateSwatchHighlights()
    {
        for (int i = 0; i < _swatchEntries.Count; i++)
        {
            var entry = _swatchEntries[i];
            bool match = Math.Abs(entry.Color.R - _selectedColor.R) <= 2 &&
                         Math.Abs(entry.Color.G - _selectedColor.G) <= 2 &&
                         Math.Abs(entry.Color.B - _selectedColor.B) <= 2;

            entry.Border.BorderBrush = match ? SwatchActiveBorder : SwatchNormalBorder;
            entry.Border.BorderThickness = match ? new Thickness(2.0) : new Thickness(1.2);
        }
    }

    private void SpectrumBars_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSpectrumThumbs();
    }

    private void HueBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingHue = true;
        HueBarContainer.CaptureMouse();
        UpdateHueFromMouse(e.GetPosition(HueBarContainer).X);
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingHue) return;
        UpdateHueFromMouse(e.GetPosition(HueBarContainer).X);
    }

    private void HueBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingHue) return;
        _isDraggingHue = false;
        HueBarContainer.ReleaseMouseCapture();
    }

    private void UpdateHueFromMouse(double mouseX)
    {
        double width = Math.Max(1.0, HueBarContainer.ActualWidth);
        double norm = Math.Clamp(mouseX / width, 0.0, 1.0);
        _currentHue = norm * 360.0;

        // If user was on pure black or pure white, snap tone to vibrant neon center
        if (_currentTonePos < 0.08 || _currentTonePos > 0.92)
        {
            _currentTonePos = 0.5;
        }

        ApplyHsvToneSelection();
    }

    private void ToneBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTone = true;
        ToneBarContainer.CaptureMouse();
        UpdateToneFromMouse(e.GetPosition(ToneBarContainer).X);
    }

    private void ToneBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingTone) return;
        UpdateToneFromMouse(e.GetPosition(ToneBarContainer).X);
    }

    private void ToneBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingTone) return;
        _isDraggingTone = false;
        ToneBarContainer.ReleaseMouseCapture();
    }

    private void UpdateToneFromMouse(double mouseX)
    {
        double width = Math.Max(1.0, ToneBarContainer.ActualWidth);
        _currentTonePos = Math.Clamp(mouseX / width, 0.0, 1.0);
        ApplyHsvToneSelection();
    }

    private void ApplyHsvToneSelection()
    {
        double s, v;
        if (_currentTonePos <= 0.5)
        {
            s = Math.Clamp(_currentTonePos * 2.0, 0.0, 1.0);
            v = 1.0;
        }
        else
        {
            s = 1.0;
            v = Math.Clamp(1.0 - (_currentTonePos - 0.5) * 1.92, 0.04, 1.0);
        }

        _selectedColor = ColorRgb.FromHsv(_currentHue, s, v);
        UpdateUiFromColor();
        ColorChanged?.Invoke(_selectedColor);
    }

    private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally || SliderR == null || SliderG == null || SliderB == null) return;

        byte r = (byte)SliderR.Value;
        byte g = (byte)SliderG.Value;
        byte b = (byte)SliderB.Value;

        _selectedColor = new ColorRgb(r, g, b);
        SyncHsvFromColor(_selectedColor);
        UpdateUiFromColor();
        ColorChanged?.Invoke(_selectedColor);
    }

    private void HexInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally || SliderR == null || SliderG == null || SliderB == null || TonePureHueStop == null) return;

        string hex = HexInputTextBox.Text.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            _selectedColor = new ColorRgb(r, g, b);
            SyncHsvFromColor(_selectedColor);
            _isUpdatingInternally = true;
            try
            {
                SliderR.Value = r;
                SliderG.Value = g;
                SliderB.Value = b;
                TextR.Text = r.ToString();
                TextG.Text = g.ToString();
                TextB.Text = b.ToString();
                _previewBrush.Color = Color.FromRgb(r, g, b);
                RgbSummaryText.Text = $"RGB({r}, {g}, {b})";
                ColorRgb pureHue = ColorRgb.FromHsv(_currentHue, 1.0, 1.0);
                TonePureHueStop.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);
                UpdateSpectrumThumbs();
                UpdateSwatchHighlights();
            }
            finally
            {
                _isUpdatingInternally = false;
            }
            ColorChanged?.Invoke(_selectedColor);
        }
    }
}
