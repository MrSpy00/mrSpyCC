using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class SnakeEffect : EffectBase
{
    public override string Id => "snake";
    public override string DisplayName => "Yılan Oyunu (Snake)";

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    private readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int Row;
        public readonly int Col;

        public GridPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(GridPos other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object? obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Row, Col);
    }

    private const int MIN_ROW = 1;
    private const int MAX_ROW = 5;
    private const int MIN_COL = 0;
    private const int MAX_COL = 18;
    private const double AUTO_RESTART_TIME = 3.0;

    private readonly List<GridPos> _body = new();
    private GridPos _food;
    private GridPos? _goldenApple = null;
    private double _goldenAppleTimer = 0;
    private double _goldenSpawnCountdown = 8.0;

    private Direction _currentDir = Direction.Right;
    private Direction _nextDir = Direction.Right;

    private double _stepTimer = 0;
    private double _stepInterval = 0.16;
    private bool _isGameOver = false;
    private double _gameOverTimer = 0;
    private int _score = 0;
    private double _time = 0;
    private double _digestionWave = -1.0; // Index of traveling glow in body

    private static readonly Random _rng = new();

    public override void Reset()
    {
        _body.Clear();
        _body.Add(new GridPos(3, 5));
        _body.Add(new GridPos(3, 4));
        _body.Add(new GridPos(3, 3));

        _currentDir = Direction.Right;
        _nextDir = Direction.Right;
        _stepTimer = 0;
        _stepInterval = 0.16;
        _isGameOver = false;
        _gameOverTimer = 0;
        _score = 0;
        _time = 0;
        _digestionWave = -1.0;
        _goldenApple = null;
        _goldenAppleTimer = 0;
        _goldenSpawnCountdown = 8.0;

        SpawnFood();
    }

    private void SpawnFood()
    {
        for (int tries = 0; tries < 100; tries++)
        {
            int r = _rng.Next(MIN_ROW, MAX_ROW + 1);
            int c = _rng.Next(MIN_COL, MAX_COL + 1);
            var candidate = new GridPos(r, c);

            if (KeyMapTable.FindByGrid(r, c) != null && !_body.Contains(candidate))
            {
                _food = candidate;
                return;
            }
        }
        _food = new GridPos(2, 8);
    }

    private void SpawnGoldenApple()
    {
        for (int tries = 0; tries < 50; tries++)
        {
            int r = _rng.Next(MIN_ROW, MAX_ROW + 1);
            int c = _rng.Next(MIN_COL, MAX_COL + 1);
            var candidate = new GridPos(r, c);

            if (KeyMapTable.FindByGrid(r, c) != null && !_body.Contains(candidate) && !candidate.Equals(_food))
            {
                _goldenApple = candidate;
                _goldenAppleTimer = 6.0; // 6 saniye kalır
                return;
            }
        }
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

        // Sindirim dalgası ilerlemesi
        if (_digestionWave >= 0)
        {
            _digestionWave += dt * 12.0;
            if (_digestionWave >= _body.Count)
                _digestionWave = -1.0;
        }

        // Altın elma geri sayımı
        if (_goldenApple.HasValue)
        {
            _goldenAppleTimer -= dt;
            if (_goldenAppleTimer <= 0)
                _goldenApple = null;
        }
        else
        {
            _goldenSpawnCountdown -= dt;
            if (_goldenSpawnCountdown <= 0)
            {
                SpawnGoldenApple();
                _goldenSpawnCountdown = 12.0 + (_rng.NextDouble() * 8.0);
            }
        }

        _stepTimer += dt;
        if (_stepTimer >= _stepInterval)
        {
            _stepTimer = 0;
            Step();
        }
    }

    private void Step()
    {
        _currentDir = _nextDir;
        var head = _body[0];
        int newRow = head.Row;
        int newCol = head.Col;

        switch (_currentDir)
        {
            case Direction.Up: newRow--; break;
            case Direction.Down: newRow++; break;
            case Direction.Left: newCol--; break;
            case Direction.Right: newCol++; break;
        }

        // Duvar çarpışma kontrolü
        if (newRow < MIN_ROW || newRow > MAX_ROW || newCol < MIN_COL || newCol > MAX_COL)
        {
            TriggerGameOver();
            return;
        }

        // Klavye tuş boşluğu kontrolü
        var nextKey = KeyMapTable.FindByGrid(newRow, newCol);
        if (nextKey == null)
        {
            if (_currentDir == Direction.Right) newCol++;
            else if (_currentDir == Direction.Left) newCol--;
            else if (_currentDir == Direction.Down) newRow++;
            else newRow--;

            nextKey = KeyMapTable.FindByGrid(newRow, newCol);
            if (nextKey == null)
            {
                TriggerGameOver();
                return;
            }
        }

        var newHead = new GridPos(newRow, newCol);

        // Kendi kuyruğuna çarpma kontrolü
        for (int i = 0; i < _body.Count - 1; i++)
        {
            if (_body[i].Equals(newHead))
            {
                TriggerGameOver();
                return;
            }
        }

        _body.Insert(0, newHead);

        // Yem yeme kontrolü
        if (newHead.Equals(_food))
        {
            _score += 10;
            _stepInterval = Math.Max(0.08, 0.16 - (_score * 0.002));
            _digestionWave = 0.0; // Sindirim ışıltısı başlat
            SpawnFood();
        }
        else if (_goldenApple.HasValue && newHead.Equals(_goldenApple.Value))
        {
            _score += 50; // Büyük bonus
            _goldenApple = null;
            _digestionWave = 0.0;
        }
        else
        {
            _body.RemoveAt(_body.Count - 1);
        }
    }

    private void TriggerGameOver()
    {
        _isGameOver = true;
        _gameOverTimer = 0;
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu retro arcade fonu
        buffer.Fill(4, 8, 16);

        if (_isGameOver)
        {
            RenderGameOverSequence(buffer);
            return;
        }

        // 1. Normal Kırmızı Elma (Yavaşça nabız gibi atan)
        var foodKey = KeyMapTable.FindByGrid(_food.Row, _food.Col);
        if (foodKey != null)
        {
            double pulse = (Math.Sin(_time * 8.0) + 1.0) * 0.5;
            byte r = (byte)(190 + (pulse * 65));
            byte g = (byte)(15 + (pulse * 25));
            buffer.SetColor(foodKey.Offset, r, g, 20);
        }

        // 2. Altın Elma (Hızlı altın parıltı)
        if (_goldenApple.HasValue)
        {
            var goldKey = KeyMapTable.FindByGrid(_goldenApple.Value.Row, _goldenApple.Value.Col);
            if (goldKey != null)
            {
                double gPulse = (Math.Sin(_time * 16.0) + 1.0) * 0.5;
                byte br = (byte)(200 + (gPulse * 55));
                buffer.SetColor(goldKey.Offset, br, (byte)(br * 0.85), 10);
            }
        }

        // 3. Yılanın Gövdesi ve Kafası
        for (int i = _body.Count - 1; i >= 0; i--)
        {
            var segment = _body[i];
            var key = KeyMapTable.FindByGrid(segment.Row, segment.Col);
            if (key == null) continue;

            if (i == 0)
            {
                // Yılan Kafası: Zümrüt Yeşili + Baktığı yöne odaklı göz ışıltısı
                buffer.SetColor(key.Offset, 0, 255, 120);
            }
            else
            {
                // Gövde: Baştan kuyruğa doğru yumuşak gradyan
                double ratio = 1.0 - ((double)i / _body.Count);
                byte g = (byte)(70 + (ratio * 140));
                byte b = (byte)(140 + (ratio * 70));

                // Sindirim dalgası geçiyorsa o segment bembeyaz parlar
                if (_digestionWave >= 0 && Math.Abs(_digestionWave - i) < 1.0)
                {
                    buffer.SetColor(key.Offset, 255, 255, 255);
                }
                else
                {
                    buffer.SetColor(key.Offset, 0, g, b);
                }
            }
        }

        // 4. F1-F12 Skor Barı
        RenderScoreBar(buffer);
    }

    private void RenderGameOverSequence(FrameBuffer buffer)
    {
        // 3 Saniyelik ölüm sekansı: Yılan erimesi ve F-tuşlarında geri sayım
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash) buffer.Fill(180, 20, 20);
        else buffer.Fill(40, 6, 6);

        // F1-F12 Geri sayım çubuğu
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_gameOverTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < remainingBars) buffer.SetColor(fOffsets[i], 255, 220, 30);
            else buffer.SetColor(fOffsets[i], 30, 5, 5);
        }
    }

    private void RenderScoreBar(FrameBuffer buffer)
    {
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        int lit = Math.Clamp(_score / 10, 0, fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < lit) buffer.SetColor(fOffsets[i], 255, 215, 0);
            else buffer.SetColor(fOffsets[i], 15, 20, 30);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        // Yanmış durumdayken herhangi bir yön tuşuna, Space'e veya harfe basılırsa anında yeniden başla
        if (_isGameOver)
        {
            if (_gameOverTimer > 0.3)
            {
                Reset();
            }
            return;
        }

        // Ok Tuşları & WASD
        int vk = key.VirtualKey;
        if ((vk == 0x26 || vk == 'W') && _currentDir != Direction.Down)
            _nextDir = Direction.Up;
        else if ((vk == 0x28 || vk == 'S') && _currentDir != Direction.Up)
            _nextDir = Direction.Down;
        else if ((vk == 0x25 || vk == 'A') && _currentDir != Direction.Right)
            _nextDir = Direction.Left;
        else if ((vk == 0x27 || vk == 'D') && _currentDir != Direction.Left)
            _nextDir = Direction.Right;
    }
}
