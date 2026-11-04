using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class LivingEyesEffect : EffectBase
{
    public override string Id => "living_eyes";
    public override string DisplayName => "Canlı Gözler (Living Eyes)";

    private enum Emotion
    {
        Curious,
        Surprised,
        Focused,
        Sleepy,
        Winking
    }

    // Göz Merkezleri (Fiziksel Klavye Koordinatları)
    private const double LEFT_EYE_X = 4.2;
    private const double LEFT_EYE_Y = 2.4;
    private const double RIGHT_EYE_X = 8.5;
    private const double RIGHT_EYE_Y = 2.4;

    private const double EYE_RADIUS_X = 2.2;
    private const double EYE_RADIUS_Y = 1.3;

    private double _time;
    private double _idleTimer;
    private double _blinkTimer;
    private double _blinkState;
    private bool _isBlinking;
    private bool _isDoubleBlink;

    private double _currentPupilX;
    private double _currentPupilY;
    private double _saccadeX;
    private double _saccadeY;
    private double _saccadeTimer;

    private Emotion _currentEmotion = Emotion.Curious;

    private double _typingRate = 0;
    private double _surpriseTimer;
    private double _winkTimer;
    private double _lastMouseX = 0.5;
    private double _lastMouseY = 0.5;

    private static readonly Random _rng = new();

    public override void Reset()
    {
        _time = 0;
        _idleTimer = 0;
        _blinkTimer = 3.0;
        _blinkState = 0;
        _isBlinking = false;
        _isDoubleBlink = false;
        _currentPupilX = 0;
        _currentPupilY = 0;
        _saccadeX = 0;
        _saccadeY = 0;
        _saccadeTimer = 0.3;
        _currentEmotion = Emotion.Curious;
        _typingRate = 0;
        _surpriseTimer = 0;
        _winkTimer = 0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _idleTimer += dt;
        _blinkTimer -= dt;
        _typingRate = Math.Max(0.0, _typingRate - (dt * 2.0));

        // 1. Mouse Hareketi ve Takibi
        MouseTracker.GetNormalizedPosition(out double mx, out double my);
        double mouseSpeed = Math.Sqrt(Math.Pow(mx - _lastMouseX, 2) + Math.Pow(my - _lastMouseY, 2));
        if (mouseSpeed > 0.003)
        {
            _lastMouseX = mx;
            _lastMouseY = my;
            _idleTimer = 0;

            if (mouseSpeed > 0.15)
            {
                // Ani fare hareketi: Şaşkınlık refleksi
                _surpriseTimer = 0.8;
                _currentEmotion = Emotion.Surprised;
            }
        }

        // 2. Doğal Biyolojik Sakkadlar (Micro-saccades & Fixation Jitter)
        _saccadeTimer -= dt;
        if (_saccadeTimer <= 0)
        {
            _saccadeTimer = 0.2 + (_rng.NextDouble() * 0.4);
            if (_idleTimer > 3.0 && _idleTimer < 8.0)
            {
                // Fare sabitken klavyedeki rastgele tuşlara merakla bakınma
                _saccadeX = (_rng.NextDouble() - 0.5) * 1.4;
                _saccadeY = (_rng.NextDouble() - 0.5) * 0.8;
            }
            else
            {
                // Doğal canlı göz seğirmesi
                _saccadeX = (_rng.NextDouble() - 0.5) * 0.25;
                _saccadeY = (_rng.NextDouble() - 0.5) * 0.15;
            }
        }

        // İstenen göz bebeği hedefi
        double desiredX = ((mx - 0.5) * 1.8) + _saccadeX;
        double desiredY = ((my - 0.5) * 1.2) + _saccadeY;

        // Duygu Yönetimi
        if (_surpriseTimer > 0)
        {
            _surpriseTimer -= dt;
            _currentEmotion = Emotion.Surprised;
            desiredX *= 0.4;
            desiredY *= 0.4;
        }
        else if (_winkTimer > 0)
        {
            _winkTimer -= dt;
            _currentEmotion = Emotion.Winking;
        }
        else if (_typingRate > 3.0)
        {
            _currentEmotion = Emotion.Focused;
        }
        else if (_idleTimer > 7.0)
        {
            _currentEmotion = Emotion.Sleepy;
        }
        else
        {
            _currentEmotion = Emotion.Curious;
            // Arada bir çapkınca tek göz kırpma (Wink)
            if (_rng.NextDouble() < 0.002)
            {
                _winkTimer = 0.9;
            }
        }

        // Göz bebeği yumuşak fiziksel takibi
        double lerpSpeed = _currentEmotion == Emotion.Surprised ? 18.0 : 9.0;
        _currentPupilX += (desiredX - _currentPupilX) * Math.Min(1.0, dt * lerpSpeed);
        _currentPupilY += (desiredY - _currentPupilY) * Math.Min(1.0, dt * lerpSpeed);

        // 3. Göz Kırpma Refleksi (Tekli veya Çift Kırpma)
        if (!_isBlinking && _blinkTimer <= 0)
        {
            _isBlinking = true;
            _isDoubleBlink = _rng.NextDouble() < 0.3; // %30 çift göz kırpma
            _blinkTimer = 2.0 + (_rng.NextDouble() * 3.5);
        }

        if (_isBlinking)
        {
            double blinkSpeed = _currentEmotion == Emotion.Sleepy ? 6.0 : 13.0;
            _blinkState += dt * blinkSpeed;
            if (_blinkState >= 1.0)
            {
                if (_isDoubleBlink)
                {
                    _isDoubleBlink = false;
                    _blinkState = 0;
                }
                else
                {
                    _blinkState = 0;
                    _isBlinking = false;
                }
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        bool isSleeping = _idleTimer > 12.0;
        double sleepPhase = Math.Sin(_time * 1.2) * 0.5 + 0.5;

        // Arka plan: Derin siber uzay ambiyansı
        byte bgR = (byte)(isSleeping ? 2 : 4);
        byte bgG = (byte)(isSleeping ? 4 : 8);
        byte bgB = (byte)(isSleeping ? (10 + (sleepPhase * 8)) : 18);
        buffer.Fill(bgR, bgG, bgB);

        if (isSleeping)
        {
            RenderSleepingEyes(buffer, sleepPhase);
            RenderZzz(buffer);
            return;
        }

        // Gözlerin açıklık miktarı (Duyguya ve göz kırpmaya göre)
        double openAmount = 1.0;
        if (_isBlinking)
        {
            openAmount = 1.0 - Math.Abs(Math.Sin(_blinkState * Math.PI));
        }

        if (_currentEmotion == Emotion.Surprised)
        {
            openAmount = Math.Min(1.3, openAmount + 0.3); // Fal taşı gibi açılan gözler
        }
        else if (_currentEmotion == Emotion.Sleepy)
        {
            openAmount *= 0.55; // Ağırlaşmış yarı açık göz kapakları
        }
        else if (_currentEmotion == Emotion.Focused)
        {
            openAmount *= 0.8; // Odaklanmış kedi gözü
        }

        // Sol Göz
        RenderSingleEye(buffer, LEFT_EYE_X, LEFT_EYE_Y, _currentPupilX, _currentPupilY, openAmount, isRightEye: false);

        // Sağ Göz (Wink modunda sağ göz kapalı hilal olur)
        double rightOpen = _currentEmotion == Emotion.Winking ? 0.05 : openAmount;
        RenderSingleEye(buffer, RIGHT_EYE_X, RIGHT_EYE_Y, _currentPupilX, _currentPupilY, rightOpen, isRightEye: true);
    }

    private void RenderSingleEye(FrameBuffer buffer, double eyeCenterX, double eyeCenterY, double pupX, double pupY, double openAmount, bool isRightEye)
    {
        double currentRadiusY = Math.Max(0.2, EYE_RADIUS_Y * openAmount);

        foreach (var key in KeyMapTable.AllKeys)
        {
            double kx = key.X + (key.Width * 0.5);
            double ky = key.Y + (key.Height * 0.5);

            double dx = (kx - eyeCenterX) / EYE_RADIUS_X;
            double dy = (ky - eyeCenterY) / currentRadiusY;
            double distSq = (dx * dx) + (dy * dy);

            if (distSq <= 1.0)
            {
                // Gözün İçi!
                double px = (kx - (eyeCenterX + pupX * 0.6)) / 0.85;
                double py = (ky - (eyeCenterY + pupY * 0.4)) / (0.85 * currentRadiusY);
                double pupilDistSq = (px * px) + (py * py);

                if (pupilDistSq <= 0.65)
                {
                    // Göz Bebeği ve İris Dokusu
                    if (pupilDistSq <= 0.15)
                    {
                        // Işık Parıltısı (Catchlight)
                        buffer.SetColor(key.Offset, 240, 250, 255);
                    }
                    else if (pupilDistSq <= 0.40)
                    {
                        // Göz Bebeği Merkezi (Koyu / Siyah)
                        buffer.SetColor(key.Offset, 15, 10, 30);
                    }
                    else
                    {
                        // İris Halkası (Duyguya göre değişir)
                        switch (_currentEmotion)
                        {
                            case Emotion.Surprised:
                                buffer.SetColor(key.Offset, 255, 220, 0); // Altın Şaşkınlık
                                break;
                            case Emotion.Focused:
                                buffer.SetColor(key.Offset, 255, 60, 20);  // Alevli Odaklanma
                                break;
                            case Emotion.Sleepy:
                                buffer.SetColor(key.Offset, 0, 180, 220);  // Loş Deniz Mavisi
                                break;
                            default:
                                buffer.SetColor(key.Offset, 130, 60, 255); // Elektrik Moru
                                break;
                        }
                    }
                }
                else
                {
                    // Göz Akı (Sclera) - Yumuşak buz mavisi
                    double edge = 1.0 - Math.Sqrt(distSq);
                    byte r = (byte)Math.Clamp(170 + (edge * 70), 0, 255);
                    byte g = (byte)Math.Clamp(215 + (edge * 40), 0, 255);
                    buffer.SetColor(key.Offset, r, g, 255);
                }
            }
            else if (distSq <= 1.6)
            {
                // Göz Kapağı ve Kaş Çevresi Parlaması
                double glow = (1.6 - distSq) / 0.6;
                buffer.BlendPixel(key.Offset, 80, 40, 160, (byte)(glow * 130));
            }
        }
    }

    private void RenderSleepingEyes(FrameBuffer buffer, double sleepPhase)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            double kx = key.X + (key.Width * 0.5);
            double ky = key.Y + (key.Height * 0.5);

            double dl = Math.Sqrt(Math.Pow(kx - LEFT_EYE_X, 2) + Math.Pow((ky - LEFT_EYE_Y) * 3.0, 2));
            if (dl < 1.6)
            {
                byte bb = (byte)(180 * sleepPhase);
                buffer.SetColor(key.Offset, 20, 80, bb);
            }

            double dr = Math.Sqrt(Math.Pow(kx - RIGHT_EYE_X, 2) + Math.Pow((ky - RIGHT_EYE_Y) * 3.0, 2));
            if (dr < 1.6)
            {
                byte bb = (byte)(180 * sleepPhase);
                buffer.SetColor(key.Offset, 20, 80, bb);
            }
        }
    }

    private void RenderZzz(FrameBuffer buffer)
    {
        double zTime = (_time * 0.8) % 3.0;
        double zY = 2.0 - zTime;
        double zX = RIGHT_EYE_X + 2.0 + Math.Sin(zTime * 2.0) * 0.8;

        var key = KeyMapTable.FindNearest(zX, zY);
        if (key != null)
        {
            buffer.SetColor(key.Offset, 80, 180, 255);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            _idleTimer = 0; // Uyan
            _typingRate += 1.0;
            _surpriseTimer = 0.5; // Heyecanlan
            _blinkTimer += 0.8;
        }
    }
}
