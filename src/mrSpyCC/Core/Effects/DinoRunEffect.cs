using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class DinoRunEffect : EffectBase
{
    public override string Id => "dino_run";
    public override string DisplayName => "Dinozor Koşusu (Dino T-Rex)";

    private enum ObstacleType
    {
        SmallCactus,
        DoubleCactus,
        Pterodactyl
    }

    private sealed class Obstacle
    {
        public double X;
        public double Y;
        public ObstacleType Type;
    }

    private const double FLOOR_Y = 4.0;
    private const double DINO_X = 2.2;
    private const double BASE_GRAVITY = 28.0;
    private const double LOW_GRAVITY = 16.0; // Variable jump hold
    private const double JUMP_VELOCITY = -9.8;
    private const double AUTO_RESTART_TIME = 3.0;

    private double _dinoY = FLOOR_Y;
    private double _verticalVelocity = 0;
    private bool _isJumping = false;
    private bool _jumpHeld = false;

    private double _gameSpeed = 8.5;
    private double _score = 0;
    private int _highScore = 0;
    private bool _isGameOver = false;
    private double _gameOverTimer = 0;
    private double _time = 0;

    private double _spawnTimer = 1.4;
    private readonly List<Obstacle> _obstacles = new();
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _dinoY = FLOOR_Y;
        _verticalVelocity = 0;
        _isJumping = false;
        _jumpHeld = false;
        _gameSpeed = 8.5;
        _score = 0;
        _isGameOver = false;
        _gameOverTimer = 0;
        _spawnTimer = 1.4;
        _obstacles.Clear();
    }

    public override void Update(double dt)
    {
        _time += dt;

        if (_isGameOver)
        {
            _gameOverTimer += dt;
            // 3 Saniye kuralı: 3 saniye sonra otomatik sıfırlama ve yeniden başlama
            if (_gameOverTimer >= AUTO_RESTART_TIME)
            {
                Reset();
            }
            return;
        }

        // Skor ve hız artışı
        _score += dt * 10.0;
        _gameSpeed = 8.5 + Math.Min(14.0, _score * 0.06);

        // Dinamik zıplama fiziği (Variable Jump Arc)
        if (_isJumping)
        {
            _dinoY += _verticalVelocity * dt;
            double effectiveGravity = _jumpHeld && _verticalVelocity < 0 ? LOW_GRAVITY : BASE_GRAVITY;
            _verticalVelocity += effectiveGravity * dt;

            if (_dinoY >= FLOOR_Y)
            {
                _dinoY = FLOOR_Y;
                _isJumping = false;
                _verticalVelocity = 0;
            }
        }

        // Engel üretimi (Kaktüs, Çift Kaktüs, Pterodactyl)
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            double r = _rng.NextDouble();
            ObstacleType type;
            double spawnY = FLOOR_Y;

            if (_score > 60 && r < 0.35)
            {
                type = ObstacleType.Pterodactyl;
                spawnY = 2.6; // Havada uçan kuş
            }
            else if (r < 0.70)
            {
                type = ObstacleType.SmallCactus;
            }
            else
            {
                type = ObstacleType.DoubleCactus;
            }

            _obstacles.Add(new Obstacle
            {
                X = 21.0,
                Y = spawnY,
                Type = type
            });

            // Bir sonraki engele kadar rastgele süre (hıza göre ölçeklenir)
            _spawnTimer = (1.1 + (_rng.NextDouble() * 1.3)) * (8.5 / _gameSpeed);
        }

        // Engelleri hareket ettir ve çarpışma kontrolü
        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            var obs = _obstacles[i];
            obs.X -= _gameSpeed * dt;

            // Çarpışma kontrolü
            double hitWidth = obs.Type == ObstacleType.DoubleCactus ? 1.1 : 0.65;
            double dx = Math.Abs(obs.X - DINO_X);
            double dy = Math.Abs(obs.Y - _dinoY);

            if (dx < hitWidth && dy < 0.75)
            {
                // Çarpışma! Game Over
                _isGameOver = true;
                _gameOverTimer = 0;
                if ((int)_score > _highScore)
                    _highScore = (int)_score;
                return;
            }

            if (obs.X < -2.0)
            {
                _obstacles.RemoveAt(i);
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // 1. Retro çöl gecesi derin fonu
        buffer.Fill(4, 5, 12);

        // 2. Parallaks Gece Gökyüzü (Row 0 üzerinde süzülen yıldızlar ve ay)
        RenderParallaxSky(buffer);

        if (_isGameOver)
        {
            RenderGameOverSequence(buffer);
            return;
        }

        // 3. Zemin Pisti (Row 5 üzerinde koşan çöl yolu)
        foreach (var key in KeyMapTable.AllKeys)
        {
            if (key.Row == 5)
            {
                // Hareketli zemin dokusu
                int shimmer = (int)(Math.Sin((key.X * 1.5) - (_time * 12.0)) * 15.0);
                buffer.SetColor(key.Offset, (byte)Math.Clamp(20 + shimmer, 0, 255), (byte)Math.Clamp(35 + shimmer, 0, 255), (byte)Math.Clamp(65 + shimmer, 0, 255));
            }
        }

        // 4. Engelleri Çiz
        foreach (var obs in _obstacles)
        {
            var obsKey = KeyMapTable.FindNearest(obs.X, obs.Y);
            if (obsKey != null)
            {
                switch (obs.Type)
                {
                    case ObstacleType.Pterodactyl:
                        // Pterodactyl: Parlak Neon Fuşya/Mor kanatlı uçan canlı
                        buffer.SetColor(obsKey.Offset, 255, 0, 150);
                        break;
                    case ObstacleType.DoubleCactus:
                        // İkili Kaktüs: Alev Kırmızısı çift tuş
                        buffer.SetColor(obsKey.Offset, 255, 30, 0);
                        var neighbor = KeyMapTable.FindNearest(obs.X + 0.9, obs.Y);
                        if (neighbor != null && neighbor.Offset != obsKey.Offset)
                            buffer.SetColor(neighbor.Offset, 230, 60, 0);
                        break;
                    case ObstacleType.SmallCactus:
                    default:
                        // Tekli Kaktüs: Parlak Turuncu
                        buffer.SetColor(obsKey.Offset, 255, 75, 0);
                        break;
                }
            }
        }

        // 5. Dino T-Rex (Zümrüt Yeşili / Elektrik Camgöbeği)
        var dinoKey = KeyMapTable.FindNearest(DINO_X, _dinoY);
        if (dinoKey != null)
        {
            buffer.SetColor(dinoKey.Offset, 0, 255, 140);
        }

        // Dinozor gövde/kuyruk izi
        if (!_isJumping)
        {
            var tailKey = KeyMapTable.FindNearest(DINO_X - 0.75, FLOOR_Y);
            if (tailKey != null && tailKey.Offset != dinoKey?.Offset)
            {
                buffer.SetColor(tailKey.Offset, 0, 180, 100);
            }
        }
        else
        {
            // Zıplarken arkasında bıraktığı itici rüzgar
            var trailKey = KeyMapTable.FindNearest(DINO_X, _dinoY + 0.8);
            if (trailKey != null && trailKey.Offset != dinoKey?.Offset)
            {
                buffer.SetColor(trailKey.Offset, 0, 120, 200);
            }
        }

        // 6. F1-F12 Skor Çubuğu
        RenderScoreBar(buffer);
    }

    private void RenderParallaxSky(FrameBuffer buffer)
    {
        // F tuşları dışındaki Row 0 tuşlarında (Esc, PrtSc, Delete vb.) yıldız ışıltıları
        foreach (var key in KeyMapTable.AllKeys)
        {
            if (key.Row == 0 && (key.Col < 1 || key.Col > 12))
            {
                double starPhase = Math.Sin((key.X * 2.1) + (_time * 1.5));
                if (starPhase > 0.4)
                {
                    byte b = (byte)(80 + (starPhase * 120));
                    buffer.SetColor(key.Offset, b, b, (byte)Math.Min(255, b + 50));
                }
            }
        }
    }

    private void RenderGameOverSequence(FrameBuffer buffer)
    {
        // 3 Saniyelik ölüm sekansı: Kırmızı flaş ve F tuşlarında 3-2-1 geri sayım
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash)
        {
            buffer.Fill(180, 20, 20);
        }
        else
        {
            buffer.Fill(50, 8, 8);
        }

        // Çarpışma noktasında beyaz parlama
        var deadDino = KeyMapTable.FindNearest(DINO_X, _dinoY);
        if (deadDino != null)
            buffer.SetColor(deadDino.Offset, 255, 255, 255);

        // F1-F12 üzerinde 3 saniyelik geri sayım barı
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_gameOverTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < remainingBars)
            {
                // Kalan süre: Altın sarısı / Beyaz geri sayım barı
                buffer.SetColor(fOffsets[i], 255, 220, 30);
            }
            else
            {
                // Sönen barlar: Koyu kırmızı
                buffer.SetColor(fOffsets[i], 40, 5, 5);
            }
        }
    }

    private void RenderScoreBar(FrameBuffer buffer)
    {
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        int litCount = Math.Clamp((int)(_score / 15.0), 0, fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < litCount)
            {
                buffer.SetColor(fOffsets[i], 255, 215, 0); // Altın skor
            }
            else
            {
                buffer.SetColor(fOffsets[i], 15, 20, 35);
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        // Space or Up Arrow = Jump or Restart
        if (key.VirtualKey == 0x20 || key.VirtualKey == 0x26)
        {
            _jumpHeld = isDown;

            if (isDown)
            {
                if (_isGameOver)
                {
                    // 0.3 saniyelik debouncedan sonra anında baştan başlama
                    if (_gameOverTimer > 0.3)
                    {
                        Reset();
                    }
                }
                else if (!_isJumping)
                {
                    _isJumping = true;
                    _verticalVelocity = JUMP_VELOCITY;
                }
            }
        }
        else if (isDown && _isGameOver && _gameOverTimer > 0.3)
        {
            // Yanmışken herhangi bir tuşa basılırsa da beklemeden anında yeniden başlasın
            Reset();
        }
    }
}
