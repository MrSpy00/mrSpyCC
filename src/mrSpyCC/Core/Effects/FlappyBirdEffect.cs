using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class FlappyBirdEffect : EffectBase
{
    public override string Id => "flappy_bird";
    public override string DisplayName => "Uçan Kuş (Cyber Flappy)";

    private sealed class PipePair
    {
        public double X;
        public double GapY; // Boşluğun dikey merkezi (1.5 .. 3.8)
        public bool Passed = false;
    }

    private const double BIRD_X = 3.2;
    private const double GRAVITY = 20.0;
    private const double FLAP_VELOCITY = -7.5;
    private const double PIPE_SPEED = 7.0;
    private const double GAP_HEIGHT = 1.6;
    private const double AUTO_RESTART_TIME = 3.0;

    private double _birdY = 2.5;
    private double _birdVy = 0;
    private bool _isGameOver = false;
    private double _gameOverTimer = 0;
    private int _score = 0;
    private double _time = 0;

    private double _spawnTimer = 1.8;
    private readonly List<PipePair> _pipes = new();
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _birdY = 2.5;
        _birdVy = 0;
        _isGameOver = false;
        _gameOverTimer = 0;
        _score = 0;
        _time = 0;
        _spawnTimer = 1.8;
        _pipes.Clear();
    }

    public override void Update(double dt)
    {
        _time += dt;

        if (_isGameOver)
        {
            _gameOverTimer += dt;
            // 3 Saniye kuralı: 3 saniye sonra otomatik yeniden başlama
            if (_gameOverTimer >= AUTO_RESTART_TIME)
            {
                Reset();
            }
            return;
        }

        // Kuş Yerçekimi Fiziği
        _birdVy += GRAVITY * dt;
        _birdY += _birdVy * dt;

        // Tavan ve Taban Çarpışması
        if (_birdY <= 0.2 || _birdY >= 5.2)
        {
            TriggerGameOver();
            return;
        }

        // Boru Üretimi
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            _spawnTimer = 2.2;
            _pipes.Add(new PipePair
            {
                X = 21.0,
                GapY = 1.6 + (_rng.NextDouble() * 2.2)
            });
        }

        // Boruları Hareket Ettir ve Çarpışma Kontrolü
        for (int i = _pipes.Count - 1; i >= 0; i--)
        {
            var p = _pipes[i];
            p.X -= PIPE_SPEED * dt;

            // Skor Kontrolü
            if (!p.Passed && p.X < BIRD_X)
            {
                p.Passed = true;
                _score++;
            }

            // Çarpışma Kontrolü
            if (Math.Abs(p.X - BIRD_X) < 0.7)
            {
                // Kuş boşluğun içinde mi?
                double topLimit = p.GapY - (GAP_HEIGHT * 0.5);
                double bottomLimit = p.GapY + (GAP_HEIGHT * 0.5);

                if (_birdY < topLimit || _birdY > bottomLimit)
                {
                    TriggerGameOver();
                    return;
                }
            }

            if (p.X < -2.0)
            {
                _pipes.RemoveAt(i);
            }
        }
    }

    private void TriggerGameOver()
    {
        _isGameOver = true;
        _gameOverTimer = 0;
    }

    public override void Render(FrameBuffer buffer)
    {
        // Gökyüzü gecesi derin mavisi
        buffer.Fill(3, 6, 16);

        if (_isGameOver)
        {
            RenderGameOver(buffer);
            return;
        }

        // 1. Yaklaşan Borular (Neon Yeşil)
        foreach (var p in _pipes)
        {
            double topLimit = p.GapY - (GAP_HEIGHT * 0.5);
            double bottomLimit = p.GapY + (GAP_HEIGHT * 0.5);

            foreach (var key in KeyMapTable.AllKeys)
            {
                double kx = key.X + (key.Width * 0.5);
                double ky = key.Y + (key.Height * 0.5);

                if (Math.Abs(kx - p.X) < 0.65)
                {
                    if (ky < topLimit || ky > bottomLimit)
                    {
                        buffer.SetColor(key.Offset, 0, 220, 60); // Boru Gövdesi
                    }
                }
            }
        }

        // 2. Flappy Bird (Altın Sarısı / Neon Turuncu)
        var birdKey = KeyMapTable.FindNearest(BIRD_X, _birdY);
        if (birdKey != null)
        {
            buffer.SetColor(birdKey.Offset, 255, 215, 0); // Altın Kuş
        }

        // Kanat çırpma izi
        if (_birdVy < 0)
        {
            var wingKey = KeyMapTable.FindNearest(BIRD_X - 0.7, _birdY + 0.3);
            if (wingKey != null && wingKey.Offset != birdKey?.Offset)
            {
                buffer.SetColor(wingKey.Offset, 255, 120, 0);
            }
        }

        // 3. F1-F12 Skor
        RenderScoreOnFKeys(buffer);
    }

    private void RenderGameOver(FrameBuffer buffer)
    {
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash) buffer.Fill(180, 20, 30);
        else buffer.Fill(40, 5, 8);

        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_gameOverTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < remainingBars) buffer.SetColor(fOffsets[i], 255, 220, 30);
            else buffer.SetColor(fOffsets[i], 30, 5, 5);
        }
    }

    private void RenderScoreOnFKeys(FrameBuffer buffer)
    {
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        int lit = Math.Clamp(_score, 0, fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < lit) buffer.SetColor(fOffsets[i], 255, 215, 0);
            else buffer.SetColor(fOffsets[i], 15, 20, 30);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        // Space veya Yukarı Ok = Kanat Çırp veya Yeniden Başla
        if (key.VirtualKey == 0x20 || key.VirtualKey == 0x26)
        {
            if (_isGameOver)
            {
                if (_gameOverTimer > 0.3)
                    Reset();
            }
            else
            {
                _birdVy = FLAP_VELOCITY;
            }
        }
        else if (_isGameOver && _gameOverTimer > 0.3)
        {
            Reset();
        }
    }
}
