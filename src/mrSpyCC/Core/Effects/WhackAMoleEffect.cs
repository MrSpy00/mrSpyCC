using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class WhackAMoleEffect : EffectBase
{
    public override string Id => "whack_a_mole";
    public override string DisplayName => "Köstebek Vurmaca (Cyber Whack)";

    private const double MOLE_DURATION = 1.3;
    private const double AUTO_RESTART_TIME = 3.0;

    private KeyEntry? _currentMoleKey;
    private double _moleTimer = 0;
    private double _nextMoleDelay = 0.5;

    private int _score = 0;
    private int _strikes = 0;
    private bool _isGameOver = false;
    private double _gameOverTimer = 0;

    private KeyEntry? _hitEffectKey;
    private double _hitEffectTimer = 0;

    private static readonly Random _rng = new();

    public override void Reset()
    {
        _currentMoleKey = null;
        _moleTimer = 0;
        _nextMoleDelay = 0.5;
        _score = 0;
        _strikes = 0;
        _isGameOver = false;
        _gameOverTimer = 0;
        _hitEffectKey = null;
        _hitEffectTimer = 0;
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

        // Vurma ışıltısı zamanlayıcısı
        if (_hitEffectTimer > 0)
            _hitEffectTimer -= dt;

        // Köstebek aktifse süresini azalt
        if (_currentMoleKey != null)
        {
            _moleTimer -= dt;
            if (_moleTimer <= 0)
            {
                // Süre bitti, köstebek kaçtı! 1 Ceza puanı
                _currentMoleKey = null;
                _strikes++;
                if (_strikes >= 3)
                {
                    _isGameOver = true;
                    _gameOverTimer = 0;
                }
                else
                {
                    _nextMoleDelay = 0.4 + (_rng.NextDouble() * 0.6);
                }
            }
        }
        else
        {
            // Yeni köstebek çıkarma gecikmesi
            _nextMoleDelay -= dt;
            if (_nextMoleDelay <= 0)
            {
                SpawnMole();
            }
        }
    }

    private void SpawnMole()
    {
        var all = KeyMapTable.AllKeys;
        for (int i = 0; i < 50; i++)
        {
            var k = all[_rng.Next(all.Count)];
            // Sadece ana harf ve rakam satırlarında çıksın (Row 1..4)
            if (k.Row >= 1 && k.Row <= 4 && k.VirtualKey >= 'A' && k.VirtualKey <= 'Z' || (k.VirtualKey >= '0' && k.VirtualKey <= '9'))
            {
                _currentMoleKey = k;
                _moleTimer = Math.Max(0.7, MOLE_DURATION - (_score * 0.03)); // Skor arttıkça hızlanır
                return;
            }
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu toprak / neon zemin
        buffer.Fill(5, 4, 10);

        if (_isGameOver)
        {
            RenderGameOver(buffer);
            return;
        }

        // 1. Köstebek Yuvaları (Loş mavi halkalar)
        // 2. Aktif Köstebek (Parlak Fuşya / Neon Pembe Nabız)
        if (_currentMoleKey != null)
        {
            double progress = _moleTimer / MOLE_DURATION;
            byte r = (byte)(200 + (progress * 55));
            buffer.SetColor(_currentMoleKey.Offset, r, 0, 180);
        }

        // 3. Vurulan Tuş Başarı Patlaması (Zümrüt Yeşili)
        if (_hitEffectTimer > 0 && _hitEffectKey != null)
        {
            buffer.SetColor(_hitEffectKey.Offset, 0, 255, 120);
        }

        // 4. Canlar (F1..F3) ve Skor (F5..F12)
        RenderHud(buffer);
    }

    private void RenderHud(FrameBuffer buffer)
    {
        // F1..F3: Kalan Haklar (3 can, kaybedildikçe kırmızı yanar)
        int[] strikesF = [45, 69, 93];
        for (int i = 0; i < strikesF.Length; i++)
        {
            if (i < (3 - _strikes)) buffer.SetColor(strikesF[i], 0, 240, 100); // Yeşil Hak
            else buffer.SetColor(strikesF[i], 255, 20, 20);                  // Kayıp Kırmızı
        }

        // F5..F12: Skor
        int[] scoreF = [141, 165, 189, 213, 237, 261, 285, 309];
        int lit = Math.Clamp(_score / 2, 0, scoreF.Length);
        for (int i = 0; i < scoreF.Length; i++)
        {
            if (i < lit) buffer.SetColor(scoreF[i], 255, 215, 0);
            else buffer.SetColor(scoreF[i], 15, 20, 30);
        }
    }

    private void RenderGameOver(FrameBuffer buffer)
    {
        bool flash = ((int)(_gameOverTimer * 8.0)) % 2 == 0;
        if (flash) buffer.Fill(180, 20, 20);
        else buffer.Fill(40, 5, 5);

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
        if (!isDown) return;

        if (_isGameOver)
        {
            if (_gameOverTimer > 0.3)
                Reset();
            return;
        }

        if (_currentMoleKey != null)
        {
            if (key.Offset == _currentMoleKey.Offset || key.VirtualKey == _currentMoleKey.VirtualKey)
            {
                // Tam isabet! "WHACK!"
                _score++;
                _hitEffectKey = _currentMoleKey;
                _hitEffectTimer = 0.35;
                _currentMoleKey = null;
                _nextMoleDelay = 0.3 + (_rng.NextDouble() * 0.4);
            }
            else
            {
                // Hatalı tuş basışı!
                _strikes++;
                if (_strikes >= 3)
                {
                    _isGameOver = true;
                    _gameOverTimer = 0;
                }
            }
        }
    }
}
