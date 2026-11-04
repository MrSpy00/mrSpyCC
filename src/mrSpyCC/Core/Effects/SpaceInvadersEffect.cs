using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class SpaceInvadersEffect : EffectBase
{
    public override string Id => "space_invaders";
    public override string DisplayName => "Uzay İstilacıları (Space Invaders)";

    private sealed class Alien
    {
        public double X;
        public double Y;
        public bool IsAlive = true;
        public bool IsLeader;
    }

    private sealed class Projectile
    {
        public double X;
        public double Y;
        public bool IsAlien;
    }

    private sealed class Bunker
    {
        public double X;
        public double Y;
        public int Health = 3; // 3=Green, 2=Yellow, 1=Red, 0=Destroyed
    }

    private sealed class Explosion
    {
        public double X;
        public double Y;
        public double Life = 0.35;
    }

    private const double SHIP_Y = 4.8;
    private const double SHIP_SPEED = 12.0;
    private const double AUTO_RESTART_TIME = 3.0;

    private double _shipX = 9.0;
    private double _alienDir = 1.0;
    private double _alienStepTimer = 0;
    private double _alienStepInterval = 0.45;
    private double _alienOffsetX = 0;
    private double _alienOffsetY = 0;
    private double _alienBombTimer = 1.5;

    // Mystery Mothership (UFO) on row 0
    private double _ufoX = -5.0;
    private bool _ufoActive = false;
    private double _ufoSpawnTimer = 14.0;

    private readonly List<Alien> _aliens = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Bunker> _bunkers = new();
    private readonly List<Explosion> _explosions = new();
    private static readonly Random _rng = new();

    private bool _moveLeft = false;
    private bool _moveRight = false;
    private bool _isGameOver = false;
    private double _gameOverTimer = 0;
    private int _score = 0;

    public override void Reset()
    {
        _shipX = 9.0;
        _alienDir = 1.0;
        _alienStepTimer = 0;
        _alienStepInterval = 0.45;
        _alienOffsetX = 0;
        _alienOffsetY = 0;
        _alienBombTimer = 1.5;
        _ufoX = -5.0;
        _ufoActive = false;
        _ufoSpawnTimer = 14.0;
        _moveLeft = false;
        _moveRight = false;
        _isGameOver = false;
        _gameOverTimer = 0;
        _score = 0;
        _projectiles.Clear();
        _explosions.Clear();

        SpawnAlienFleet();
        SpawnBunkers();
    }

    private void SpawnAlienFleet()
    {
        _aliens.Clear();
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                _aliens.Add(new Alien
                {
                    X = 3.5 + (col * 2.2),
                    Y = 1.2 + (row * 1.0),
                    IsLeader = row == 0
                });
            }
        }
    }

    private void SpawnBunkers()
    {
        _bunkers.Clear();
        _bunkers.Add(new Bunker { X = 4.5, Y = 3.8, Health = 3 });
        _bunkers.Add(new Bunker { X = 9.5, Y = 3.8, Health = 3 });
        _bunkers.Add(new Bunker { X = 14.5, Y = 3.8, Health = 3 });
    }

    public override void Update(double dt)
    {
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

        // Gemi hareketi
        if (_moveLeft) _shipX = Math.Max(1.0, _shipX - (SHIP_SPEED * dt));
        if (_moveRight) _shipX = Math.Min(18.0, _shipX + (SHIP_SPEED * dt));

        // Gizemli UFO hareketi
        if (_ufoActive)
        {
            _ufoX += 9.0 * dt;
            if (_ufoX > 22.0)
            {
                _ufoActive = false;
                _ufoSpawnTimer = 15.0 + (_rng.NextDouble() * 10.0);
            }
        }
        else
        {
            _ufoSpawnTimer -= dt;
            if (_ufoSpawnTimer <= 0)
            {
                _ufoActive = true;
                _ufoX = -2.0;
            }
        }

        // Uzaylıların bomba fırlatması
        _alienBombTimer -= dt;
        if (_alienBombTimer <= 0)
        {
            _alienBombTimer = 1.2 + (_rng.NextDouble() * 1.5);
            List<Alien> aliveAliens = new();
            foreach (var a in _aliens) if (a.IsAlive) aliveAliens.Add(a);

            if (aliveAliens.Count > 0)
            {
                var shooter = aliveAliens[_rng.Next(aliveAliens.Count)];
                _projectiles.Add(new Projectile
                {
                    X = shooter.X + _alienOffsetX,
                    Y = shooter.Y + _alienOffsetY + 0.5,
                    IsAlien = true
                });
            }
        }

        // Mermilerin güncellenmesi
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];

            if (p.IsAlien)
            {
                // Uzaylı bombası aşağı düşer
                p.Y += 8.0 * dt;

                // Sığınak vurma kontrolü
                bool hitBunker = false;
                foreach (var b in _bunkers)
                {
                    if (b.Health > 0 && Math.Abs(p.X - b.X) < 1.0 && Math.Abs(p.Y - b.Y) < 0.6)
                    {
                        b.Health--;
                        hitBunker = true;
                        _explosions.Add(new Explosion { X = b.X, Y = b.Y });
                        break;
                    }
                }

                // Gemi vurma kontrolü
                if (Math.Abs(p.X - _shipX) < 1.0 && Math.Abs(p.Y - SHIP_Y) < 0.6)
                {
                    // Oyuncu vuruldu!
                    _isGameOver = true;
                    _gameOverTimer = 0;
                    _explosions.Add(new Explosion { X = _shipX, Y = SHIP_Y });
                    return;
                }

                if (hitBunker || p.Y > 5.5)
                {
                    _projectiles.RemoveAt(i);
                }
            }
            else
            {
                // Oyuncu lazeri yukarı çıkar
                p.Y -= 14.0 * dt;

                // Sığınak vurma kontrolü (dost ateşi sığınağa zarar vermez, geçer)
                // Uzaylı vurma kontrolü
                bool hit = false;
                foreach (var a in _aliens)
                {
                    if (!a.IsAlive) continue;
                    double ax = a.X + _alienOffsetX;
                    double ay = a.Y + _alienOffsetY;

                    if (Math.Abs(p.X - ax) < 0.8 && Math.Abs(p.Y - ay) < 0.6)
                    {
                        a.IsAlive = false;
                        hit = true;
                        _score++;
                        _explosions.Add(new Explosion { X = ax, Y = ay });
                        break;
                    }
                }

                // UFO vurma kontrolü
                if (!hit && _ufoActive && Math.Abs(p.X - _ufoX) < 1.2 && p.Y < 1.0)
                {
                    _ufoActive = false;
                    _score += 10; // Büyük puan
                    hit = true;
                    _explosions.Add(new Explosion { X = _ufoX, Y = 0.5 });
                }

                if (hit || p.Y < 0.5)
                {
                    _projectiles.RemoveAt(i);
                }
            }
        }

        // Patlamaların güncellenmesi
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Life -= dt;
            if (_explosions[i].Life <= 0)
                _explosions.RemoveAt(i);
        }

        // Uzaylı filosu yürüyüşü
        _alienStepTimer += dt;
        if (_alienStepTimer >= _alienStepInterval)
        {
            _alienStepTimer = 0;
            _alienOffsetX += _alienDir * 0.45;

            if (_alienOffsetX > 3.0 || _alienOffsetX < -2.0)
            {
                _alienDir = -_alienDir;
                _alienOffsetY += 0.5; // Aşağı in
                _alienStepInterval = Math.Max(0.18, _alienStepInterval - 0.02);
            }
        }

        // İstilacıların tabana ulaşıp ulaşmadığı
        bool anyAlive = false;
        foreach (var a in _aliens)
        {
            if (a.IsAlive)
            {
                anyAlive = true;
                double ay = a.Y + _alienOffsetY;
                if (ay >= SHIP_Y - 0.5)
                {
                    _isGameOver = true;
                    _gameOverTimer = 0;
                    return;
                }
            }
        }

        // Tüm uzaylılar öldüyse yeni filo spawn et
        if (!anyAlive)
        {
            _alienOffsetY = 0;
            _alienOffsetX = 0;
            _alienStepInterval = Math.Max(0.2, _alienStepInterval * 0.85);
            SpawnAlienFleet();
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu uzay boşluğu fonu
        buffer.Fill(3, 3, 10);

        if (_isGameOver)
        {
            RenderGameOverSequence(buffer);
            return;
        }

        // 1. UFO (Mothership - Row 0 üzerinde uçan kırmızı amiral gemisi)
        if (_ufoActive)
        {
            var ufoKey = KeyMapTable.FindNearest(_ufoX, 0.4);
            if (ufoKey != null)
            {
                buffer.SetColor(ufoKey.Offset, 255, 30, 80);
            }
        }

        // 2. Uzaylılar
        foreach (var a in _aliens)
        {
            if (!a.IsAlive) continue;
            double ax = a.X + _alienOffsetX;
            double ay = a.Y + _alienOffsetY;

            var key = KeyMapTable.FindNearest(ax, ay);
            if (key != null)
            {
                if (a.IsLeader)
                    buffer.SetColor(key.Offset, 255, 0, 140); // Neon Magenta Lider
                else
                    buffer.SetColor(key.Offset, 0, 255, 80);  // Parlak Yeşil Dron
            }
        }

        // 3. Savunma Sığınakları (Bunkers)
        foreach (var b in _bunkers)
        {
            if (b.Health <= 0) continue;
            var key = KeyMapTable.FindNearest(b.X, b.Y);
            if (key != null)
            {
                switch (b.Health)
                {
                    case 3: buffer.SetColor(key.Offset, 0, 220, 100); break; // Sağlam: Yeşil
                    case 2: buffer.SetColor(key.Offset, 220, 200, 0); break;   // Hasarlı: Sarı
                    case 1: buffer.SetColor(key.Offset, 255, 50, 0); break;    // Kritik: Kırmızı
                }
            }
        }

        // 4. Mermiler & Bombalar
        foreach (var p in _projectiles)
        {
            var key = KeyMapTable.FindNearest(p.X, p.Y);
            if (key != null)
            {
                if (p.IsAlien)
                    buffer.SetColor(key.Offset, 255, 0, 80); // Kırmızı Alien Bombası
                else
                    buffer.SetColor(key.Offset, 255, 255, 255); // Ak Lazer
            }
        }

        // 5. Patlamalar
        foreach (var exp in _explosions)
        {
            var key = KeyMapTable.FindNearest(exp.X, exp.Y);
            if (key != null)
            {
                buffer.SetColor(key.Offset, 255, 200, 0);
            }
        }

        // 6. Oyuncu Gemisi (Neon Cyan)
        var shipCenter = KeyMapTable.FindNearest(_shipX, SHIP_Y);
        var shipWingL = KeyMapTable.FindNearest(_shipX - 0.9, SHIP_Y);
        var shipWingR = KeyMapTable.FindNearest(_shipX + 0.9, SHIP_Y);

        if (shipCenter != null) buffer.SetColor(shipCenter.Offset, 0, 240, 255);
        if (shipWingL != null) buffer.SetColor(shipWingL.Offset, 0, 180, 255);
        if (shipWingR != null) buffer.SetColor(shipWingR.Offset, 0, 180, 255);

        // 7. F1-F12 Skor
        RenderScoreOnFKeys(buffer);
    }

    private void RenderGameOverSequence(FrameBuffer buffer)
    {
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash) buffer.Fill(180, 20, 40);
        else buffer.Fill(35, 5, 10);

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
        int vk = key.VirtualKey;

        if (vk == 0x25 || vk == 'A') _moveLeft = isDown;
        if (vk == 0x27 || vk == 'D') _moveRight = isDown;

        if (isDown)
        {
            if (_isGameOver)
            {
                if (_gameOverTimer > 0.3)
                    Reset();
                return;
            }

            if (vk == 0x20) // Space = Ateş
            {
                int playerBullets = 0;
                foreach (var p in _projectiles) if (!p.IsAlien) playerBullets++;

                if (playerBullets < 3)
                {
                    _projectiles.Add(new Projectile { X = _shipX, Y = SHIP_Y - 0.8, IsAlien = false });
                }
            }
        }
    }
}
