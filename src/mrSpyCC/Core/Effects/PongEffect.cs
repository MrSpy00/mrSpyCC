using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class PongEffect : EffectBase
{
    public override string Id => "pong";
    public override string DisplayName => "Retro Tenis (Pong)";

    private const double PADDLE_HEIGHT = 1.8;
    private const double LEFT_X = 1.2;
    private const double RIGHT_X = 18.2;
    private const double TOP_Y = 1.0;
    private const double BOTTOM_Y = 5.0;
    private const int WINNING_SCORE = 5;
    private const double AUTO_RESTART_TIME = 3.0;

    private double _leftPaddleY = 3.0;
    private double _rightPaddleY = 3.0;

    private double _ballX = 10.0;
    private double _ballY = 3.0;
    private double _ballVx = 8.5;
    private double _ballVy = 4.0;

    private bool _moveUp = false;
    private bool _moveDown = false;

    private int _leftScore = 0;
    private int _rightScore = 0;
    private bool _isMatchOver = false;
    private double _matchOverTimer = 0;

    public override void Reset()
    {
        _leftPaddleY = 3.0;
        _rightPaddleY = 3.0;
        _leftScore = 0;
        _rightScore = 0;
        _moveUp = false;
        _moveDown = false;
        _isMatchOver = false;
        _matchOverTimer = 0;
        ResetBall(1.0);
    }

    private void ResetBall(double dir)
    {
        _ballX = 10.0;
        _ballY = 3.0;
        _ballVx = 8.0 * dir;
        _ballVy = (Random.Shared.NextDouble() - 0.5) * 6.0;
    }

    public override void Update(double dt)
    {
        if (_isMatchOver)
        {
            _matchOverTimer += dt;
            // 3 Saniye kuralı: 3 saniye sonra otomatik yeniden başlama
            if (_matchOverTimer >= AUTO_RESTART_TIME)
            {
                Reset();
            }
            return;
        }

        // 1. Sağ Raket (Oyuncu)
        if (_moveUp) _rightPaddleY = Math.Max(TOP_Y + (PADDLE_HEIGHT * 0.5), _rightPaddleY - (9.5 * dt));
        if (_moveDown) _rightPaddleY = Math.Min(BOTTOM_Y - (PADDLE_HEIGHT * 0.5), _rightPaddleY + (9.5 * dt));

        // 2. Sol Raket (Yapay Zeka)
        double dy = _ballY - _leftPaddleY;
        _leftPaddleY += Math.Sign(dy) * Math.Min(Math.Abs(dy), 7.2 * dt);
        _leftPaddleY = Math.Clamp(_leftPaddleY, TOP_Y + (PADDLE_HEIGHT * 0.5), BOTTOM_Y - (PADDLE_HEIGHT * 0.5));

        // 3. Top Fiziği
        _ballX += _ballVx * dt;
        _ballY += _ballVy * dt;

        // Üst ve alt kenardan sekme
        if (_ballY <= TOP_Y)
        {
            _ballY = TOP_Y;
            _ballVy = Math.Abs(_ballVy);
        }
        else if (_ballY >= BOTTOM_Y)
        {
            _ballY = BOTTOM_Y;
            _ballVy = -Math.Abs(_ballVy);
        }

        // Sol raketten sekme (Falso ve hızlanma)
        if (_ballX <= LEFT_X + 0.6 && _ballX >= LEFT_X - 0.6)
        {
            double diff = _ballY - _leftPaddleY;
            if (Math.Abs(diff) < (PADDLE_HEIGHT * 0.65))
            {
                _ballVx = Math.Abs(_ballVx) * 1.05;
                _ballVy = diff * 5.0; // Kenara çarptıkça falso açısı artar
            }
        }

        // Sağ raketten sekme (Falso ve hızlanma)
        if (_ballX >= RIGHT_X - 0.6 && _ballX <= RIGHT_X + 0.6)
        {
            double diff = _ballY - _rightPaddleY;
            if (Math.Abs(diff) < (PADDLE_HEIGHT * 0.65))
            {
                _ballVx = -Math.Abs(_ballVx) * 1.05;
                _ballVy = diff * 5.0;
            }
        }

        // Skor Kontrolü
        if (_ballX < 0.0)
        {
            _rightScore++;
            if (_rightScore >= WINNING_SCORE)
            {
                _isMatchOver = true;
                _matchOverTimer = 0;
            }
            else
            {
                ResetBall(-1.0);
            }
        }
        else if (_ballX > 20.0)
        {
            _leftScore++;
            if (_leftScore >= WINNING_SCORE)
            {
                _isMatchOver = true;
                _matchOverTimer = 0;
            }
            else
            {
                ResetBall(1.0);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu retro kort fonu
        buffer.Fill(4, 6, 14);

        if (_isMatchOver)
        {
            RenderMatchOver(buffer);
            return;
        }

        // Orta File Çizgisi
        foreach (var key in KeyMapTable.AllKeys)
        {
            if (Math.Abs((key.X + key.Width * 0.5) - 10.0) < 0.6)
            {
                buffer.SetColor(key.Offset, 15, 25, 45);
            }
        }

        // 1. Sol Raket (Yapay Zeka - Elektrik Camgöbeği)
        var lp1 = KeyMapTable.FindNearest(LEFT_X, _leftPaddleY - 0.6);
        var lp2 = KeyMapTable.FindNearest(LEFT_X, _leftPaddleY + 0.6);
        if (lp1 != null) buffer.SetColor(lp1.Offset, 0, 240, 255);
        if (lp2 != null) buffer.SetColor(lp2.Offset, 0, 240, 255);

        // 2. Sağ Raket (Oyuncu - Parlak Pembe)
        var rp1 = KeyMapTable.FindNearest(RIGHT_X, _rightPaddleY - 0.6);
        var rp2 = KeyMapTable.FindNearest(RIGHT_X, _rightPaddleY + 0.6);
        if (rp1 != null) buffer.SetColor(rp1.Offset, 255, 30, 160);
        if (rp2 != null) buffer.SetColor(rp2.Offset, 255, 30, 160);

        // 3. Top (Bembeyaz Ak Kor)
        var ballKey = KeyMapTable.FindNearest(_ballX, _ballY);
        if (ballKey != null)
        {
            buffer.SetColor(ballKey.Offset, 255, 255, 255);
        }

        // 4. F Tuşları Skor Tabelası
        int[] leftF = [45, 69, 93, 117];
        int[] rightF = [237, 261, 285, 309];

        for (int i = 0; i < leftF.Length; i++)
        {
            if (i < _leftScore) buffer.SetColor(leftF[i], 0, 240, 255);
            else buffer.SetColor(leftF[i], 10, 20, 30);
        }

        for (int i = 0; i < rightF.Length; i++)
        {
            if (i < _rightScore) buffer.SetColor(rightF[i], 255, 30, 160);
            else buffer.SetColor(rightF[i], 30, 10, 20);
        }
    }

    private void RenderMatchOver(FrameBuffer buffer)
    {
        bool flash = ((int)(_matchOverTimer * 8.0)) % 2 == 0;
        bool rightWon = _rightScore >= WINNING_SCORE;

        if (flash)
        {
            if (rightWon) buffer.Fill(180, 20, 100); // Oyuncu kazandı: Pembe zafer
            else buffer.Fill(0, 120, 180);          // AI kazandı: Mavi zafer
        }
        else
        {
            buffer.Fill(20, 10, 20);
        }

        // F1-F12 üzerinde geri sayım
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_matchOverTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < remainingBars) buffer.SetColor(fOffsets[i], 255, 220, 30);
            else buffer.SetColor(fOffsets[i], 30, 5, 15);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        int vk = key.VirtualKey;
        if (vk == 0x26 || vk == 'W') _moveUp = isDown;
        if (vk == 0x28 || vk == 'S') _moveDown = isDown;

        if (isDown && _isMatchOver && _matchOverTimer > 0.3)
        {
            Reset();
        }
    }
}
