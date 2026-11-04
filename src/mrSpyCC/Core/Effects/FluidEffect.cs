using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🌊 Akışkan Fiziği & Biyolüminesans Okyanus Dalgası (Fluid Dynamics)
/// Gerçekçi yüzey gerilimi, dalga paketi girişimleri (constructive interference),
/// serbest yayılım ve tuş vuruşu su damlası simülasyonu.
/// </summary>
public sealed class FluidEffect : EffectBase
{
    public override string Id => "fluid";
    public override string DisplayName => "Akışkan Fiziği";

    private sealed class Ripple
    {
        public double X;
        public double Y;
        public double Age;
        public double Amplitude;
        public double Speed;
        public double MaxAge;
    }

    private readonly List<Ripple> _ripples = new(24);
    private readonly Random _rng = new();
    private double _time;
    private double _ambientDropTimer;

    public double Speed { get; set; } = 1.0;
    public double Viscosity { get; set; } = 1.0;

    public override void Reset()
    {
        _ripples.Clear();
        _time = 0;
        _ambientDropTimer = 0;
    }

    public override void Update(double dt)
    {
        double effectiveDt = dt * Speed;
        _time += effectiveDt;
        _ambientDropTimer += effectiveDt;

        // Her 0.6 - 1.0 saniyede bir rastgele doğal yağmur damlası düşer
        if (_ambientDropTimer > 0.75)
        {
            _ambientDropTimer = 0;
            SpawnRipple(_rng.Next(1, 21), _rng.Next(0, 6), amplitude: 0.65, speed: 6.5);
        }

        // Aktif dalgaları güncelle ve sönümlenenleri temizle
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var r = _ripples[i];
            r.Age += effectiveDt;
            if (r.Age >= r.MaxAge)
            {
                _ripples.RemoveAt(i);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        double t = _time;

        foreach (var key in KeyMapTable.AllKeys)
        {
            double kx = key.Col;
            double ky = key.Row;

            // 1. Doğal ortam akıntısı ve su çalkantısı (Ambient sinusoidal swell)
            double swell1 = Math.Sin((kx * 0.32) - (t * 1.8)) * Math.Cos((ky * 0.65) - (t * 1.2)) * 0.22;
            double swell2 = Math.Sin(((kx + ky) * 0.28) - (t * 2.2)) * 0.16;
            double totalHeight = swell1 + swell2;

            // 2. Yayılan dairesel dalga paketlerinin süperpozisyonu (Radial Wave Superposition)
            for (int i = 0; i < _ripples.Count; i++)
            {
                var rip = _ripples[i];
                double dx = kx - rip.X;
                double dy = (ky - rip.Y) * 1.65; // Klavye en-boy oranına göre eliptik yayılım
                double dist = Math.Sqrt(dx * dx + dy * dy);

                double waveFront = rip.Age * rip.Speed;
                double distDiff = dist - waveFront;

                // Dalga halkası kalınlığı ve Gaussian zarfı
                if (Math.Abs(distDiff) < 3.2)
                {
                    double decay = Math.Exp(-rip.Age * 1.25 * Viscosity);
                    double gaussian = Math.Exp(-(distDiff * distDiff) / 2.2);
                    double oscillation = Math.Cos(distDiff * 2.4);

                    totalHeight += rip.Amplitude * oscillation * gaussian * decay;
                }
            }

            // 3. Optik Su Renk Haritalama (Deep Abyss -> Azure -> Electric Cyan -> White Foam)
            ColorRgb color = MapFluidColor(totalHeight);
            buffer.SetKey(key.Offset, color);
        }
    }

    private static ColorRgb MapFluidColor(double h)
    {
        // h: [-0.4 .. +1.2]
        if (h <= -0.15)
        {
            // Derin Çukur (Abyssal Midnight Blue)
            double t = Math.Clamp((h + 0.5) / 0.35, 0.0, 1.0);
            byte r = (byte)(2 + t * 4);
            byte g = (byte)(8 + t * 18);
            byte b = (byte)(28 + t * 40);
            return new ColorRgb(r, g, b);
        }
        else if (h <= 0.25)
        {
            // Orta Seviye Su Akışı (Sapphire / Royal Ocean Blue)
            double t = (h + 0.15) / 0.40;
            byte r = (byte)(6 + t * 0);
            byte g = (byte)(26 + t * 110);
            byte b = (byte)(68 + t * 150);
            return new ColorRgb(r, g, b);
        }
        else if (h <= 0.70)
        {
            // Yükselen Dalga Sırtı (Electric Cyan / Tropical Turquoise)
            double t = (h - 0.25) / 0.45;
            byte r = (byte)(0 + t * 50);
            byte g = (byte)(136 + t * 95);
            byte b = (byte)(218 + t * 37);
            return new ColorRgb(r, g, b);
        }
        else
        {
            // Dalga Tepesi & Köpük (Bright Seafoam White-Cyan Crest)
            double t = Math.Clamp((h - 0.70) / 0.50, 0.0, 1.0);
            byte r = (byte)(50 + t * 195);
            byte g = (byte)(231 + t * 24);
            byte b = 255;
            return new ColorRgb(r, g, b);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            // Tuşa basıldığında güçlü ve canlı bir su damlası şoku oluştur
            SpawnRipple(key.Col, key.Row, amplitude: 1.35, speed: 7.8);
        }
    }

    private void SpawnRipple(double x, double y, double amplitude, double speed)
    {
        if (_ripples.Count >= 20)
        {
            _ripples.RemoveAt(0);
        }

        _ripples.Add(new Ripple
        {
            X = x,
            Y = y,
            Age = 0,
            Amplitude = amplitude,
            Speed = speed,
            MaxAge = 2.4
        });
    }
}
