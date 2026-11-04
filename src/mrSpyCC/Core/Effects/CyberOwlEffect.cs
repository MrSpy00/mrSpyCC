using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class CyberOwlEffect : EffectBase
{
    public override string Id => "cyber_owl";
    public override string DisplayName => "Siber Gece Baykuşu (Cyber Owl)";

    private const double OWL_CENTER_X = 9.5;
    private const double OWL_CENTER_Y = 1.0;
    private const double LEFT_EYE_X = 8.6;
    private const double RIGHT_EYE_X = 10.4;
    private const double EYE_Y = 1.0;

    private double _time;
    private double _idleTimer;
    private double _typingExcitement = 0;
    private double _wingSpan = 0;
    private double _blinkTimer = 3.0;
    private double _blinkState = 0;
    private bool _isBlinking = false;

    private double _lookX = 0;
    private double _lookY = 0;
    private double _lastMouseX = 0.5;
    private double _lastMouseY = 0.5;

    private static readonly Random _rng = new();

    public override void Reset()
    {
        _time = 0;
        _idleTimer = 0;
        _typingExcitement = 0;
        _wingSpan = 0;
        _blinkTimer = 3.0;
        _blinkState = 0;
        _isBlinking = false;
        _lookX = 0;
        _lookY = 0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _idleTimer += dt;
        _typingExcitement = Math.Max(0.0, _typingExcitement - (dt * 1.5));

        // Kanat açıklığı hedefi (Yazı yazdıkça kanatlarını açar)
        double targetWingSpan = Math.Min(8.0, _typingExcitement * 2.5);
        _wingSpan += (targetWingSpan - _wingSpan) * Math.Min(1.0, dt * 6.0);

        // Mouse takibi (Geniş açı bakışı)
        MouseTracker.GetNormalizedPosition(out double mx, out double my);
        if (Math.Abs(mx - _lastMouseX) > 0.005 || Math.Abs(my - _lastMouseY) > 0.005)
        {
            _lastMouseX = mx;
            _lastMouseY = my;
            _idleTimer = 0;
        }

        double desiredLookX = (mx - 0.5) * 1.6;
        double desiredLookY = (my - 0.5) * 1.0;

        _lookX += (desiredLookX - _lookX) * Math.Min(1.0, dt * 8.0);
        _lookY += (desiredLookY - _lookY) * Math.Min(1.0, dt * 8.0);

        // Göz Kırpma Refleksi
        _blinkTimer -= dt;
        if (!_isBlinking && _blinkTimer <= 0)
        {
            _isBlinking = true;
            _blinkTimer = 3.0 + (_rng.NextDouble() * 4.0);
        }

        if (_isBlinking)
        {
            _blinkState += dt * 10.0;
            if (_blinkState >= 1.0)
            {
                _blinkState = 0;
                _isBlinking = false;
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Derin gece ormanı fonu
        buffer.Fill(3, 4, 10);

        bool isSleeping = _idleTimer > 15.0;
        double breath = Math.Sin(_time * 2.0) * 0.15;

        // 1. Kanatlar (Row 1..3 boyunca açılan altın/mor tüyler)
        if (_wingSpan > 0.5)
        {
            foreach (var key in KeyMapTable.AllKeys)
            {
                if (key.Row >= 1 && key.Row <= 3)
                {
                    double dx = Math.Abs(key.X - OWL_CENTER_X);
                    if (dx > 1.2 && dx <= _wingSpan)
                    {
                        double featherPhase = Math.Sin((dx * 1.5) - (_time * 8.0));
                        byte wr = (byte)Math.Clamp(120 + (featherPhase * 60), 0, 255);
                        byte wg = (byte)Math.Clamp(40 + (featherPhase * 30), 0, 255);
                        byte wb = (byte)Math.Clamp(200 + (featherPhase * 55), 0, 255);
                        buffer.BlendPixel(key.Offset, wr, wg, wb, (byte)(Math.Clamp((_wingSpan - dx) / 2.0, 0.0, 1.0) * 180));
                    }
                }
            }
        }

        // 2. Baykuşun Gövdesi ve Başı
        var bodyKey = KeyMapTable.FindNearest(OWL_CENTER_X, OWL_CENTER_Y + 1.0);
        if (bodyKey != null)
        {
            // Gövde tüyleri: Koyu gece mavisi / mor
            buffer.SetColor(bodyKey.Offset, 45, 20, 80);
        }

        // 3. Kocaman Hipnotik Sarı/Amber Gözler
        double openAmount = 1.0;
        if (_isBlinking)
        {
            openAmount = 1.0 - Math.Abs(Math.Sin(_blinkState * Math.PI));
        }
        if (isSleeping)
        {
            openAmount = 0.1;
        }

        RenderOwlEye(buffer, LEFT_EYE_X, EYE_Y, _lookX, _lookY, openAmount);
        RenderOwlEye(buffer, RIGHT_EYE_X, EYE_Y, _lookX, _lookY, openAmount);

        // 4. Gaga (Ortada küçük kehribar ışık)
        var beakKey = KeyMapTable.FindNearest(OWL_CENTER_X, EYE_Y + 0.6);
        if (beakKey != null)
        {
            buffer.SetColor(beakKey.Offset, 255, 140, 0);
        }
    }

    private void RenderOwlEye(FrameBuffer buffer, double eyeX, double eyeY, double lx, double ly, double openAmount)
    {
        var key = KeyMapTable.FindNearest(eyeX, eyeY);
        if (key == null) return;

        if (openAmount < 0.25)
        {
            // Kapalı göz (Uykulu çizgi)
            buffer.SetColor(key.Offset, 80, 60, 10);
            return;
        }

        // İris ve Göz Bebeği
        double pupDist = Math.Sqrt(Math.Pow(lx, 2) + Math.Pow(ly, 2));
        if (pupDist > 0.4)
        {
            // Kenara bakan göz: Altın sarısı iris
            buffer.SetColor(key.Offset, 255, 200, 0);
        }
        else
        {
            // Merkeze bakan göz: Işıltılı kehribar
            buffer.SetColor(key.Offset, 255, 235, 30);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _idleTimer = 0;
            _typingExcitement = Math.Min(4.0, _typingExcitement + 1.2); // Kanat çırp
        }
    }
}
