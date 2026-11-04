using System;
using mrSpyCC.Core.Effects;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Engine;

public enum EffectColorMode
{
    Default = 0,        // Efektin Kendi Orijinal Renkleri
    SingleColor = 1,    // Tek Özel Renk (1-Renk)
    DualColor = 2,      // Çift Özel Renk (2-Renk)
    RainbowRgb = 3,     // Tam RGB Gökkuşağı Modu
    StudioPalette = 4   // Küratörlü Çoklu Renk Stüdyo Paleti
}

public enum DualColorBlendMode
{
    SmartTone = 0,          // Akıllı Ton & Yoğunluk Ayrımı
    HorizontalGradient = 1, // Sol -> Sağ Yatay Geçiş
    VerticalRadial = 2,     // Üst -> Alt & Merkez Geçiş
    AlternatingWave = 3     // Dalgalı İkili Akış
}

public enum RgbFlowStyle
{
    WaveDiagonal = 0,   // Akıcı Çapraz RGB Dalgası
    SpectrumSync = 1,   // Senkron Tüm Klavye RGB Döngüsü
    RadialPinwheel = 2  // Merkezden Dönen RGB Spiral
}

/// <summary>
/// Evrensel Renk, Çift Renk, Stüdyo Paleti ve Dinamik RGB İşleme Motoru (aegisSoft).
/// Mevcut 47+ efektin tamamına ve gelecekte eklenecek tüm IEffect animasyonlarına
/// sıfır bellek tahsisatı (zero-allocation) ile gerçek zamanlı renk dönüşümü uygular.
/// </summary>
public sealed class GlobalColorProcessor
{
    public static readonly ColorRgb[][] StudioPalettes =
    [
        // 0: Cyberpunk 2077 (Cyber Yellow -> Electric Cyan -> Neon Magenta -> Deep Violet)
        [new(252, 238, 9), new(0, 240, 255), new(255, 0, 85), new(124, 58, 237)],
        // 1: aegisSoft Obsidian (Royal Purple -> Electric Violet -> Cyber Cyan -> Indigo)
        [new(124, 58, 237), new(192, 132, 252), new(0, 240, 255), new(59, 130, 246)],
        // 2: Miami Synthwave (Hot Pink -> Sunset Orange -> Golden Sun -> Deep Purple)
        [new(255, 0, 128), new(255, 110, 0), new(255, 210, 0), new(115, 25, 210)],
        // 3: Kuzey Kutbu / Nordic Aurora (Emerald -> Glacial Cyan -> Deep Azure -> Mint)
        [new(0, 255, 135), new(0, 225, 255), new(30, 80, 220), new(110, 255, 210)],
        // 4: Volkanik Magma (Crimson Lava -> Molten Orange -> Solar Gold -> Ember Red)
        [new(255, 20, 0), new(255, 115, 0), new(255, 215, 0), new(190, 0, 30)],
        // 5: Tokyo Gece Yarısı (Sakura Neon -> Cobalt Blue -> Electric Aqua -> Lavender)
        [new(255, 42, 133), new(59, 130, 246), new(6, 182, 212), new(168, 85, 247)],
        // 6: Zümrüt Matrix (Phosphor Green -> Deep Terminal -> Cyber Lime -> Mint White)
        [new(0, 255, 65), new(0, 145, 35), new(132, 255, 20), new(180, 255, 210)],
        // 7: Kraliyet Altını / Royal Gold (Pure Gold -> Warm Amber -> Champagne -> Bronze)
        [new(245, 158, 11), new(255, 210, 60), new(254, 243, 199), new(180, 83, 9)]
    ];

    public static readonly string[] StudioPaletteNames =
    [
        "Cyberpunk 2077",
        "aegisSoft Obsidian",
        "Miami Synthwave",
        "Kuzey Kutbu (Aurora)",
        "Volkanik Magma",
        "Tokyo Gece Yarısı",
        "Zümrüt Matrix",
        "Kraliyet Altını"
    ];

    public static (string Name, ColorRgb[] Colors) GetPalette(int index)
    {
        int clamped = Math.Clamp(index, 0, StudioPalettes.Length - 1);
        return (StudioPaletteNames[clamped], StudioPalettes[clamped]);
    }

    private readonly double[] _hues;
    private readonly double[] _sats;
    private readonly double[] _vals;
    private double _time;

    public EffectColorMode Mode { get; set; } = EffectColorMode.Default;
    public ColorRgb PrimaryColor { get; set; } = new(124, 58, 237);   // 1. Ana Renk (aegisSoft Purple)
    public ColorRgb SecondaryColor { get; set; } = new(0, 240, 255); // 2. İkincil Renk (Cyber Cyan)
    public DualColorBlendMode DualBlendMode { get; set; } = DualColorBlendMode.SmartTone;
    public RgbFlowStyle RgbStyle { get; set; } = RgbFlowStyle.WaveDiagonal;
    public int StudioPaletteIndex { get; set; } = 0;
    public double RgbSpeed { get; set; } = 1.0;

    public GlobalColorProcessor()
    {
        int count = KeyMapTable.AllKeys.Count;
        _hues = new double[count];
        _sats = new double[count];
        _vals = new double[count];
    }

    public void ResetPhase()
    {
        _time = 0.0;
    }

    public void ProcessFrame(FrameBuffer buffer, IEffect? activeEffect, double dt)
    {
        if (activeEffect == null || activeEffect is OffEffect)
            return;

        _time += dt * Math.Clamp(RgbSpeed, 0.2, 4.0);
        if (_time > 10000.0) _time -= 10000.0;

        var mode = Mode;
        if (mode == EffectColorMode.Default)
            return;

        var keys = KeyMapTable.AllKeys;
        int count = keys.Count;

        ColorRgb c1 = PrimaryColor;
        ColorRgb c2 = SecondaryColor;

        // Özel durum: Sabit Renk (StaticEffect) için doğrudan geometrik/akış haritalama
        if (activeEffect is StaticEffect staticEff)
        {
            ProcessStaticEffect(buffer, staticEff, mode, c1, c2);
            return;
        }

        // 1. Geçiş: Mevcut karedeki tüm tuşların HSV bileşenlerini ve renk çeşitliliğini analiz et
        int litCount = 0;
        double sumV = 0.0;
        double sumSinH = 0.0;
        double sumCosH = 0.0;
        int chromaticCount = 0;

        for (int i = 0; i < count; i++)
        {
            var col = buffer.GetKey(keys[i].Offset);
            col.ToHsv(out double h, out double s, out double v);
            _hues[i] = h;
            _sats[i] = s;
            _vals[i] = v;

            if (v > 0.02)
            {
                litCount++;
                sumV += v;
                if (s > 0.18)
                {
                    double rad = h * (Math.PI / 180.0);
                    sumSinH += Math.Sin(rad);
                    sumCosH += Math.Cos(rad);
                    chromaticCount++;
                }
            }
        }

        if (litCount == 0)
            return;

        double avgV = sumV / litCount;
        double meanHue = 0.0;
        double hueCoherence = 1.0; // 1.0 = tek renkli (monokrom), <0.82 = çok renkli

        if (chromaticCount > 0)
        {
            meanHue = Math.Atan2(sumSinH, sumCosH) * (180.0 / Math.PI);
            if (meanHue < 0.0) meanHue += 360.0;
            double rLen = Math.Sqrt(sumSinH * sumSinH + sumCosH * sumCosH) / chromaticCount;
            hueCoherence = rLen;
        }

        bool isMultiHueFrame = chromaticCount >= 3 && hueCoherence < 0.88;
        // Tam klavyeyi parlak gökkuşağı/dalga ile dolduran efektler (Wave, RainbowWave, Pinwheel, Plasma, Wheel vb.)
        bool isFullFieldHueWave = litCount >= (count - 8) && avgV > 0.78 && isMultiHueFrame;

        int palIdx = Math.Clamp(StudioPaletteIndex, 0, StudioPalettes.Length - 1);
        ReadOnlySpan<ColorRgb> activePalette = StudioPalettes[palIdx];

        // 2. Geçiş: Her tuşa seçilen küresel renk modunu uygula
        for (int i = 0; i < count; i++)
        {
            double v = _vals[i];
            if (v <= 0.004)
                continue;

            var key = keys[i];
            double h = _hues[i];
            double s = _sats[i];

            ColorRgb outColor;

            switch (mode)
            {
                case EffectColorMode.SingleColor:
                {
                    if (isFullFieldHueWave)
                    {
                        // Dalga/Plazma/Çark gibi tüm tuşların parlak olduğu efektlerde dalga hareketini koru
                        double wavePhase = 0.5 + 0.5 * Math.Cos((h - meanHue) * (Math.PI / 180.0));
                        double sculptedV = (0.14 + 0.86 * Math.Pow(wavePhase, 1.45)) * v;
                        outColor = c1.Scale(sculptedV);
                        if (wavePhase > 0.92)
                        {
                            outColor = outColor.Blend(ColorRgb.White, (wavePhase - 0.92) * 3.0);
                        }
                    }
                    else
                    {
                        outColor = c1.Scale(v);
                        // Beyaz parlama çekirdeklerini (Matrix damla ucu, yıldırım merkezi, göz parıltısı) koru
                        if (v > 0.75 && s < 0.22)
                        {
                            double whitePreserve = (1.0 - (s / 0.22)) * 0.55;
                            outColor = outColor.Blend(ColorRgb.White.Scale(v), whitePreserve);
                        }
                    }
                    break;
                }

                case EffectColorMode.DualColor:
                {
                    double t = ComputeDualBlendFactor(key, h, s, v, meanHue, isMultiHueFrame, isFullFieldHueWave);
                    ColorRgb baseDual = ColorRgb.Lerp(c1, c2, t);
                    outColor = baseDual.Scale(v);

                    if (!isFullFieldHueWave && v > 0.82 && s < 0.18)
                    {
                        outColor = outColor.Blend(ColorRgb.White.Scale(v), 0.42);
                    }
                    break;
                }

                case EffectColorMode.RainbowRgb:
                {
                    double rgbHue = ComputeRainbowHue(key, h, isMultiHueFrame);
                    outColor = ColorRgb.FromHsv(rgbHue, 1.0, v);
                    break;
                }

                case EffectColorMode.StudioPalette:
                {
                    double palPos;
                    if (isFullFieldHueWave)
                    {
                        palPos = h / 360.0;
                    }
                    else if (isMultiHueFrame && s > 0.2)
                    {
                        palPos = (h / 360.0 + _time * 0.18) % 1.0;
                    }
                    else
                    {
                        palPos = (key.X * 0.042 + key.Y * 0.065 + (1.0 - v) * 0.30 - _time * 0.28) % 1.0;
                    }
                    outColor = ColorRgb.SamplePalette(activePalette, palPos).Scale(v);
                    break;
                }

                default:
                    continue;
            }

            buffer.SetKey(key.Offset, outColor);
        }
    }

    private void ProcessStaticEffect(FrameBuffer buffer, StaticEffect staticEff, EffectColorMode mode, ColorRgb c1, ColorRgb c2)
    {
        var keys = KeyMapTable.AllKeys;
        int palIdx = Math.Clamp(StudioPaletteIndex, 0, StudioPalettes.Length - 1);
        ReadOnlySpan<ColorRgb> activePalette = StudioPalettes[palIdx];

        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            ColorRgb col;

            switch (mode)
            {
                case EffectColorMode.SingleColor:
                    col = c1;
                    break;

                case EffectColorMode.DualColor:
                {
                    if (DualBlendMode == DualColorBlendMode.SmartTone && staticEff.Mode == StaticEffect.StaticLayoutMode.WasdHighlight)
                    {
                        bool isGaming = key.Group is KeyGroup.Wasd or KeyGroup.Arrows
                            || key.Name is "W" or "A" or "S" or "D" or "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight";
                        col = isGaming ? c1 : c2;
                    }
                    else
                    {
                        double t = DualBlendMode switch
                        {
                            DualColorBlendMode.VerticalRadial => Math.Clamp(key.Y / 5.0, 0.0, 1.0),
                            DualColorBlendMode.AlternatingWave => 0.5 + 0.5 * Math.Sin(key.X * 0.65 + key.Y * 0.9 - _time * 2.4),
                            _ => Math.Clamp(key.X / 19.5, 0.0, 1.0)
                        };
                        col = ColorRgb.Lerp(c1, c2, t);
                    }
                    break;
                }

                case EffectColorMode.RainbowRgb:
                {
                    double hue = ComputeRainbowHue(key, 0.0, false);
                    col = ColorRgb.FromHsv(hue, 1.0, 1.0);
                    break;
                }

                case EffectColorMode.StudioPalette:
                {
                    double pos = (key.X * 0.045 + key.Y * 0.05 - _time * 0.25) % 1.0;
                    col = ColorRgb.SamplePalette(activePalette, pos);
                    break;
                }

                default:
                    continue;
            }

            buffer.SetKey(key.Offset, col);
        }
    }

    private double ComputeDualBlendFactor(
        KeyEntry key,
        double h,
        double s,
        double v,
        double meanHue,
        bool isMultiHueFrame,
        bool isFullFieldHueWave)
    {
        switch (DualBlendMode)
        {
            case DualColorBlendMode.HorizontalGradient:
                return Math.Clamp(key.X / 19.5, 0.0, 1.0);

            case DualColorBlendMode.VerticalRadial:
                return Math.Clamp(key.Y / 5.0, 0.0, 1.0);

            case DualColorBlendMode.AlternatingWave:
                return 0.5 + 0.5 * Math.Sin(key.X * 0.75 + key.Y * 1.1 - _time * 2.8);

            default: // SmartTone
            {
                if (isFullFieldHueWave)
                {
                    return 0.5 - 0.5 * Math.Cos((h - meanHue) * (Math.PI / 180.0));
                }

                if (isMultiHueFrame && s > 0.18)
                {
                    // Çok renkli efektlerde (Oyun modu, Yılan, Dino, Gözler, DNA, Plazma)
                    // ana ton kümesini 1. Renge, karşıt ton kümesini 2. Renge eşle
                    double diffRad = (h - meanHue) * (Math.PI / 180.0);
                    return 0.5 - 0.5 * Math.Cos(diffRad);
                }

                // Tek renkli efektlerde (Yağmur, Matrix, Nefes, Tarama, Dalga Halkası)
                // parlak çekirdeği 1. Renge, izleri ve mekansal geçişi 2. Renge eşle
                double intensityMix = 1.0 - Math.Clamp((v - 0.15) / 0.85, 0.0, 1.0);
                double spatialMix = Math.Clamp(key.X / 19.5, 0.0, 1.0);
                return Math.Clamp(intensityMix * 0.68 + spatialMix * 0.32, 0.0, 1.0);
            }
        }
    }

    private double ComputeRainbowHue(KeyEntry key, double origHue, bool isMultiHueFrame)
    {
        return RgbStyle switch
        {
            RgbFlowStyle.SpectrumSync => (_time * 95.0 + (isMultiHueFrame ? origHue * 0.35 : 0.0)) % 360.0,
            RgbFlowStyle.RadialPinwheel => ((Math.Atan2(key.Y - 2.5, key.X - 10.0) * (180.0 / Math.PI)) + 360.0 + _time * 120.0) % 360.0,
            _ => (key.X * 15.0 + key.Y * 20.0 - _time * 115.0 + (isMultiHueFrame ? origHue * 0.2 : 0.0) + 36000.0) % 360.0
        };
    }
}
