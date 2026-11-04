using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class BreakoutEffect : EffectBase
{
    public override string Id => "breakout";
    public override string DisplayName => "Tuğla Kırma (Cyber Arkanoid)";

    private sealed class Brick
    {
        public double X;
        public double Y;
        public bool IsAlive = true;
        public ColorRgb Color;
        public int Points;
    }

    private sealed class Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;
        public ColorRgb Color;
    }

    private const double PADDLE_Y = 4.9;
    private const double PADDLE_WIDTH = 2.4;
    private const double AUTO_RESTART_TIME = 3.0;

    private double _paddleX = 10.0;
    private double _paddleSpeed = 14.0;
    private bool _moveLeft = false;
    private bool _moveRight = false;

    private double _ballX = 10.0;
    private double _ballY = 4.0;
    private double _ballVx = 7.0;
    private double _ballVy = -6.5;

    private int _lives = 3;
    private int _score = 0;
    private bool _isGameOver = false;
    private bool _isVictory = false;
    private double _gameOverTimer = 0;
    private double _time = 0;

    private readonly List<Brick> _bricks = new();
    private readonly List<Particle> _particles = new();
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _paddleX = 10.0;
        _moveLeft = false;
        _moveRight = false;
        _lives = 3;
        _score = 0;
        _isGameOver = false;
        _isVictory = false;
        _gameOverTimer = 0;
        _time = 0;
        _particles.Clear();

        ResetBall();
        SpawnBricks();
    }

    private void ResetBall()
    {
        _ballX = _paddleX;
        _ballY = 4.2;
        _ballVx = (_rng.NextDouble() > 0.5 ? 6.5 : -6.5);
        _ballVy = -7.0;
    }

    private void SpawnBricks()
    {
        _bricks.Clear();
        // 2 Sıra renkli tuğlalar (Row 1 ve Row 2)
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                double bx = 2.0 + (col * 1.8);
                double by = 1.0 + (row * 1.0);
                ColorRgb colr = row == 0
                    ? new ColorRgb(255, 40, 60)   // Üst sıra: Neon Kırmızı (20 Puan)
                    : new ColorRgb(0, 230, 255);  // Alt sıra: Elektrik Camgöbeği (10 Puan)

                _bricks.Add(new Brick
                {
                    X = bx,
                    Y = by,
                    Color = colr,
                    Points = row == 0 ? 20 : 10
                });
            }
        }
    }

    public override void Update(double dt)
    {
        _time += dt;

        if (_isGameOver || _isVictory)
        {
            _gameOverTimer += dt;
            // 3 Saniye kuralı: 3 saniye sonra otomatik yeniden başlama
            if (_gameOverTimer >= AUTO_RESTART_TIME)
            {
                Reset();
            }
            return;
        }

        // Raket Hareketi
        if (_moveLeft) _paddleX = Math.Max(2.0, _paddleX - (_paddleSpeed * dt));
        if (_moveRight) _paddleX = Math.Min(18.0, _paddleX + (_paddleSpeed * dt));

        // Top Fiziği
        _ballX += _ballVx * dt;
        _ballY += _ballVy * dt;

        // Sol ve Sağ Duvar Sekmesi
        if (_ballX <= 0.8)
        {
            _ballX = 0.8;
            _ballVx = Math.Abs(_ballVx);
        }
        else if (_ballX >= 19.5)
        {
            _ballX = 19.5;
            _ballVx = -Math.Abs(_ballVx);
        }

        // Tavan Sekmesi
        if (_ballY <= 0.4)
        {
            _ballY = 0.4;
            _ballVy = Math.Abs(_ballVy);
        }

        // Raket Sekmesi
        if (_ballY >= PADDLE_Y - 0.4 && _ballY <= PADDLE_Y + 0.4)
        {
            double hitOffset = _ballX - _paddleX;
            if (Math.Abs(hitOffset) <= (PADDLE_WIDTH * 0.6))
            {
                _ballVy = -Math.Abs(_ballVy) * 1.02; // Hızlanarak yukarı sek
                _ballVx += hitOffset * 4.0;         // Falso açısı ver
                _ballY = PADDLE_Y - 0.45;
            }
        }

        // Tuğla Çarpışma Kontrolü
        bool anyBricksLeft = false;
        foreach (var brick in _bricks)
        {
            if (!brick.IsAlive) continue;
            anyBricksLeft = true;

            if (Math.Abs(_ballX - brick.X) < 1.0 && Math.Abs(_ballY - brick.Y) < 0.6)
            {
                brick.IsAlive = false;
                _score += brick.Points;
                _ballVy = -_ballVy; // Sek
                SpawnBreakParticles(brick.X, brick.Y, brick.Color);
                break;
            }
        }

        // Tüm tuğlalar bitti mi? Zafer!
        if (!anyBricksLeft)
        {
            _isVictory = true;
            _gameOverTimer = 0;
            return;
        }

        // Top Aşağı Düştü mü? Can Kaybı
        if (_ballY > 5.5)
        {
            _lives--;
            if (_lives <= 0)
            {
                _isGameOver = true;
                _gameOverTimer = 0;
            }
            else
            {
                ResetBall();
            }
        }

        // Parçacıkları Güncelle
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Life -= dt;
            if (p.Life <= 0)
                _particles.RemoveAt(i);
        }
    }

    private void SpawnBreakParticles(double x, double y, ColorRgb col)
    {
        for (int i = 0; i < 6; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double spd = 2.0 + (_rng.NextDouble() * 3.5);
            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                Vx = Math.Cos(angle) * spd,
                Vy = Math.Sin(angle) * spd,
                Life = 0.4,
                Color = col
            });
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu retro arcade zemin
        buffer.Fill(3, 4, 12);

        if (_isGameOver)
        {
            RenderGameOver(buffer);
            return;
        }

        if (_isVictory)
        {
            RenderVictory(buffer);
            return;
        }

        // 1. Tuğlalar
        foreach (var b in _bricks)
        {
            if (!b.IsAlive) continue;
            var key = KeyMapTable.FindNearest(b.X, b.Y);
            if (key != null)
            {
                buffer.SetColor(key.Offset, b.Color.R, b.Color.G, b.Color.B);
            }
        }

        // 2. Parçacıklar
        foreach (var p in _particles)
        {
            var key = KeyMapTable.FindNearest(p.X, p.Y);
            if (key != null)
            {
                buffer.BlendPixel(key.Offset, p.Color.R, p.Color.G, p.Color.B, 200);
            }
        }

        // 3. Raket (Neon Yeşil)
        var padCenter = KeyMapTable.FindNearest(_paddleX, PADDLE_Y);
        var padLeft = KeyMapTable.FindNearest(_paddleX - 0.9, PADDLE_Y);
        var padRight = KeyMapTable.FindNearest(_paddleX + 0.9, PADDLE_Y);

        if (padCenter != null) buffer.SetColor(padCenter.Offset, 0, 255, 140);
        if (padLeft != null) buffer.SetColor(padLeft.Offset, 0, 200, 100);
        if (padRight != null) buffer.SetColor(padRight.Offset, 0, 200, 100);

        // 4. Seken Top (Akkor Beyaz)
        var ballKey = KeyMapTable.FindNearest(_ballX, _ballY);
        if (ballKey != null)
        {
            buffer.SetColor(ballKey.Offset, 255, 255, 255);
        }

        // 5. Kalan Canlar ve Skor (F1..F3 Can, F5..F12 Skor)
        RenderHud(buffer);
    }

    private void RenderHud(FrameBuffer buffer)
    {
        // F1..F3: Canlar (Kırmızı Kalp)
        int[] livesF = [45, 69, 93];
        for (int i = 0; i < livesF.Length; i++)
        {
            if (i < _lives) buffer.SetColor(livesF[i], 255, 30, 60);
            else buffer.SetColor(livesF[i], 25, 5, 10);
        }

        // F5..F12: Skor (Altın Barlar)
        int[] scoreF = [141, 165, 189, 213, 237, 261, 285, 309];
        int lit = Math.Clamp(_score / 30, 0, scoreF.Length);
        for (int i = 0; i < scoreF.Length; i++)
        {
            if (i < lit) buffer.SetColor(scoreF[i], 255, 215, 0);
            else buffer.SetColor(scoreF[i], 15, 20, 30);
        }
    }

    private void RenderGameOver(FrameBuffer buffer)
    {
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash) buffer.Fill(180, 20, 30);
        else buffer.Fill(35, 5, 8);

        RenderCountdownBar(buffer);
    }

    private void RenderVictory(FrameBuffer buffer)
    {
        double p = _time * 12.0;
        buffer.Fill((byte)(Math.Sin(p) * 60 + 60), (byte)(Math.Sin(p + 2) * 60 + 60), (byte)(Math.Sin(p + 4) * 60 + 60));
        RenderCountdownBar(buffer);
    }

    private void RenderCountdownBar(FrameBuffer buffer)
    {
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_gameOverTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < remainingBars) buffer.SetColor(fOffsets[i], 255, 220, 30);
            else buffer.SetColor(fOffsets[i], 30, 5, 5);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        int vk = key.VirtualKey;
        if (vk == 0x25 || vk == 'A') _moveLeft = isDown;
        if (vk == 0x27 || vk == 'D') _moveRight = isDown;

        if (isDown && (_isGameOver || _isVictory) && _gameOverTimer > 0.3)
        {
            Reset();
        }
    }
}
