using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class VirtualPetEffect : EffectBase
{
    public override string Id => "virtual_pet";
    public override string DisplayName => "Sanal Evcil Hayvan (Virtual Pet)";

    private enum Stage
    {
        Egg,
        Baby,
        Child,
        ElderDragon
    }

    private sealed class FoodCrumb
    {
        public double X;
        public double Y;
        public int Offset;
        public double Life;
    }

    private sealed class Sparkle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;
        public ColorRgb Color;
    }

    private double _petX = 7.0;
    private double _petY = 3.0;
    private double _petTargetX = 7.0;
    private double _petTargetY = 3.0;
    private double _walkTimer = 0;
    private double _jumpOffset = 0;
    private double _jumpTimer = 0;

    private double _hunger = 80.0;     // 0..100
    private double _happiness = 85.0;  // 0..100
    private double _exp = 40.0;        // 0..400+
    private Stage _stage = Stage.Egg;
    private double _levelUpCelebration = 0;

    private double _idleTimer = 0;
    private double _time = 0;
    private bool _isBeingPetted = false;

    private readonly List<FoodCrumb> _crumbs = new();
    private readonly List<Sparkle> _sparkles = new();
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _petX = 7.0;
        _petY = 3.0;
        _petTargetX = 7.0;
        _petTargetY = 3.0;
        _hunger = 80.0;
        _happiness = 85.0;
        _exp = 40.0;
        _stage = Stage.Egg;
        _levelUpCelebration = 0;
        _idleTimer = 0;
        _time = 0;
        _isBeingPetted = false;
        _crumbs.Clear();
        _sparkles.Clear();
    }

    public override void Update(double dt)
    {
        _time += dt;
        _idleTimer += dt;

        // Seviye Kutlaması
        if (_levelUpCelebration > 0)
            _levelUpCelebration -= dt;

        // Açlık ve Mutluluk Dinamiği
        _hunger = Math.Max(10.0, _hunger - (dt * 1.2));
        if (_hunger < 30.0)
            _happiness = Math.Max(10.0, _happiness - (dt * 1.8));

        // Evrim Kontrolü
        Stage newStage = _exp switch
        {
            >= 280.0 => Stage.ElderDragon,
            >= 150.0 => Stage.Child,
            >= 60.0 => Stage.Baby,
            _ => Stage.Egg
        };

        if (newStage != _stage)
        {
            _stage = newStage;
            _levelUpCelebration = 2.0; // 2 saniye zafer ışıltısı
            SpawnLevelUpSparkles();
        }

        // Yem ömrü
        for (int i = _crumbs.Count - 1; i >= 0; i--)
        {
            _crumbs[i].Life -= dt;
            if (_crumbs[i].Life <= 0)
                _crumbs.RemoveAt(i);
        }

        // Parçacıklar
        for (int i = _sparkles.Count - 1; i >= 0; i--)
        {
            var sp = _sparkles[i];
            sp.X += sp.Vx * dt;
            sp.Y += sp.Vy * dt;
            sp.Life -= dt;
            if (sp.Life <= 0)
                _sparkles.RemoveAt(i);
        }

        // Zıplama Fiziği
        if (_jumpTimer > 0)
        {
            _jumpTimer -= dt;
            _jumpOffset = Math.Sin((0.5 - _jumpTimer) / 0.5 * Math.PI) * 1.6;
            if (_jumpTimer <= 0) _jumpOffset = 0;
        }

        // Mouse ile Sevme / Okşama (Petting) Kontrolü
        MouseTracker.GetNormalizedPosition(out double mx, out double my);
        double petNormX = _petX / 20.0;
        double petNormY = _petY / 5.5;
        double mouseDist = Math.Sqrt(Math.Pow(mx - petNormX, 2) + Math.Pow(my - petNormY, 2));

        if (mouseDist < 0.16)
        {
            _isBeingPetted = true;
            _happiness = Math.Min(100.0, _happiness + (dt * 25.0));
            _exp += dt * 5.0;
            _idleTimer = 0;

            // Pembe sevgi kalpleri saç
            if (_rng.NextDouble() < 0.25)
            {
                _sparkles.Add(new Sparkle
                {
                    X = _petX + ((_rng.NextDouble() - 0.5) * 1.2),
                    Y = _petY - 0.5,
                    Vx = (_rng.NextDouble() - 0.5) * 1.0,
                    Vy = -1.8 - (_rng.NextDouble() * 1.2),
                    Life = 0.7,
                    Color = new ColorRgb(255, 50, 160)
                });
            }
            return;
        }
        else
        {
            _isBeingPetted = false;
        }

        // Uyku modu (14 saniye dokunulmazsa)
        if (_idleTimer > 14.0)
            return;

        // Yem Arama ve Üstüne Atılma (Letter Pounce)
        FoodCrumb? targetCrumb = null;
        double nearestDistSq = double.MaxValue;
        foreach (var c in _crumbs)
        {
            double d = Math.Pow(c.X - _petX, 2) + Math.Pow(c.Y - _petY, 2);
            if (d < nearestDistSq)
            {
                nearestDistSq = d;
                targetCrumb = c;
            }
        }

        if (targetCrumb != null)
        {
            _petTargetX = targetCrumb.X;
            _petTargetY = targetCrumb.Y;

            // Yem yaklaştığında üzerine atla!
            if (nearestDistSq < 1.1)
            {
                _crumbs.Remove(targetCrumb);
                _hunger = Math.Min(100.0, _hunger + 25.0);
                _happiness = Math.Min(100.0, _happiness + 20.0);
                _exp += 15.0; // Deneyim kazan
                SpawnEatSparkles(_petX, _petY);
            }
        }
        else
        {
            // Başıboş meraklı gezinme
            _walkTimer -= dt;
            if (_walkTimer <= 0)
            {
                _walkTimer = 2.0 + (_rng.NextDouble() * 3.0);
                _petTargetX = 2.0 + (_rng.NextDouble() * 12.0);
                _petTargetY = 2.0 + (_rng.NextDouble() * 2.5);
            }
        }

        // Hedefe doğru yürüme
        double speed = (targetCrumb != null ? 6.5 : 2.8) * dt;
        double dx = _petTargetX - _petX;
        double dy = _petTargetY - _petY;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));

        if (dist > 0.1)
        {
            _petX += (dx / dist) * Math.Min(dist, speed);
            _petY += (dy / dist) * Math.Min(dist, speed);
        }
    }

    private void SpawnEatSparkles(double x, double y)
    {
        for (int i = 0; i < 8; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double spd = 2.0 + (_rng.NextDouble() * 3.5);
            _sparkles.Add(new Sparkle
            {
                X = x,
                Y = y,
                Vx = Math.Cos(angle) * spd,
                Vy = Math.Sin(angle) * spd,
                Life = 0.5 + (_rng.NextDouble() * 0.3),
                Color = new ColorRgb(255, (byte)(160 + _rng.Next(95)), 20)
            });
        }
    }

    private void SpawnLevelUpSparkles()
    {
        for (int i = 0; i < 20; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double spd = 3.0 + (_rng.NextDouble() * 4.0);
            _sparkles.Add(new Sparkle
            {
                X = _petX,
                Y = _petY,
                Vx = Math.Cos(angle) * spd,
                Vy = Math.Sin(angle) * spd,
                Life = 1.0,
                Color = new ColorRgb(0, 255, 240)
            });
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        bool isSleeping = _idleTimer > 14.0;
        double curY = _petY - _jumpOffset;

        // Seviye atlama kutlaması
        if (_levelUpCelebration > 0)
        {
            double p = _time * 12.0;
            buffer.Fill((byte)(Math.Sin(p) * 60 + 60), (byte)(Math.Sin(p + 2) * 60 + 60), (byte)(Math.Sin(p + 4) * 60 + 60));
        }
        else
        {
            buffer.Fill(4, 3, 10);
        }

        // 1. Yem Kırıntıları (Altın Yıldızlar)
        foreach (var c in _crumbs)
        {
            var key = KeyMapTable.FindByOffset(c.Offset);
            if (key != null)
            {
                double pulse = Math.Sin(_time * 8.0) * 0.3 + 0.7;
                byte goldG = (byte)(215 * pulse);
                buffer.SetColor(key.Offset, 255, goldG, 0);
            }
        }

        // 2. Canlının Gövdesi (Aşamaya göre morph)
        var centerKey = KeyMapTable.FindNearest(_petX, curY);

        if (isSleeping)
        {
            if (centerKey != null)
            {
                double sleepBreath = Math.Sin(_time * 1.5) * 0.5 + 0.5;
                buffer.SetColor(centerKey.Offset, 0, (byte)(130 + sleepBreath * 60), (byte)(210 + sleepBreath * 40));
            }
        }
        else if (_isBeingPetted)
        {
            // Sevilirken sırtüstü yatıp parıldama (Mırıldanma / Purr)
            if (centerKey != null) buffer.SetColor(centerKey.Offset, 255, 60, 180);
            var leftKey = KeyMapTable.FindNearest(_petX - 0.8, curY);
            var rightKey = KeyMapTable.FindNearest(_petX + 0.8, curY);
            if (leftKey != null) buffer.SetColor(leftKey.Offset, 255, 120, 200);
            if (rightKey != null) buffer.SetColor(rightKey.Offset, 255, 120, 200);
        }
        else
        {
            RenderPetByStage(buffer, centerKey, curY);
        }

        // 3. Parçacıklar
        foreach (var sp in _sparkles)
        {
            var sk = KeyMapTable.FindNearest(sp.X, sp.Y);
            if (sk != null)
            {
                buffer.BlendPixel(sk.Offset, sp.Color.R, sp.Color.G, sp.Color.B, 200);
            }
        }

        // 4. F1-F12 Üzerinde Canlı Durum Barları (Açlık, Mutluluk, EXP)
        RenderTopHud(buffer);
    }

    private void RenderPetByStage(FrameBuffer buffer, KeyEntry? centerKey, double curY)
    {
        switch (_stage)
        {
            case Stage.Egg:
                // Yumurta: Zümrüt parıltılı tek canlı hücre
                if (centerKey != null)
                {
                    double pulse = Math.Sin(_time * 6.0) * 0.3 + 0.7;
                    buffer.SetColor(centerKey.Offset, (byte)(30 * pulse), (byte)(240 * pulse), (byte)(160 * pulse));
                }
                break;

            case Stage.Baby:
                // Bebek: Kafa ve minik gövde
                if (centerKey != null) buffer.SetColor(centerKey.Offset, 0, 255, 180);
                var babyTail = KeyMapTable.FindNearest(_petX - 0.8, curY);
                if (babyTail != null && babyTail.Offset != centerKey?.Offset)
                    buffer.SetColor(babyTail.Offset, 0, 180, 120);
                break;

            case Stage.Child:
                // Çocuk: Kafa, gövde ve sevimli kulak
                if (centerKey != null) buffer.SetColor(centerKey.Offset, 0, 240, 255);
                var childBody = KeyMapTable.FindNearest(_petX - 0.9, curY);
                if (childBody != null && childBody.Offset != centerKey?.Offset)
                    buffer.SetColor(childBody.Offset, 120, 0, 255);
                var childTail = KeyMapTable.FindNearest(_petX - 1.7, curY + 0.3);
                if (childTail != null)
                    buffer.SetColor(childTail.Offset, 255, 60, 160);
                break;

            case Stage.ElderDragon:
                // Efsanevi Siber Ejderha: 3-tuş gövde + kanatlar + neon aura
                if (centerKey != null) buffer.SetColor(centerKey.Offset, 255, 215, 0); // Altın kafa
                var dBody = KeyMapTable.FindNearest(_petX - 0.9, curY);
                if (dBody != null) buffer.SetColor(dBody.Offset, 255, 0, 120); // Magenta gövde
                var dWingU = KeyMapTable.FindNearest(_petX - 0.5, curY - 0.8);
                if (dWingU != null) buffer.SetColor(dWingU.Offset, 0, 240, 255); // Camgöbeği kanat
                var dTail = KeyMapTable.FindNearest(_petX - 1.8, curY);
                if (dTail != null) buffer.SetColor(dTail.Offset, 140, 0, 255); // Mor kuyruk
                break;
        }
    }

    private void RenderTopHud(FrameBuffer buffer)
    {
        // F1..F4: Açlık (Yeşil Bar)
        int[] hungerF = [45, 69, 93, 117];
        int hLit = Math.Clamp((int)(_hunger / 25.0), 0, hungerF.Length);
        for (int i = 0; i < hungerF.Length; i++)
        {
            if (i < hLit) buffer.SetColor(hungerF[i], 0, 240, 80);
            else buffer.SetColor(hungerF[i], 10, 30, 15);
        }

        // F5..F8: Mutluluk (Pembe Bar)
        int[] happyF = [141, 165, 189, 213];
        int hpLit = Math.Clamp((int)(_happiness / 25.0), 0, happyF.Length);
        for (int i = 0; i < happyF.Length; i++)
        {
            if (i < hpLit) buffer.SetColor(happyF[i], 255, 40, 150);
            else buffer.SetColor(happyF[i], 30, 10, 20);
        }

        // F9..F12: EXP / Evrim Aşaması (Camgöbeği Bar)
        int[] expF = [237, 261, 285, 309];
        int expLit = Math.Clamp((int)(_exp / 80.0), 0, expF.Length);
        for (int i = 0; i < expF.Length; i++)
        {
            if (i < expLit) buffer.SetColor(expF[i], 0, 220, 255);
            else buffer.SetColor(expF[i], 10, 20, 35);
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        _idleTimer = 0;

        // Space = Trambolin Zıplayışı
        if (key.VirtualKey == 0x20)
        {
            _jumpTimer = 0.5;
            _happiness = Math.Min(100.0, _happiness + 15.0);
            _exp += 8.0;

            for (int i = 0; i < 5; i++)
            {
                _sparkles.Add(new Sparkle
                {
                    X = _petX + ((_rng.NextDouble() - 0.5) * 1.5),
                    Y = _petY - 0.5,
                    Vx = (_rng.NextDouble() - 0.5) * 1.5,
                    Vy = -2.5 - (_rng.NextDouble() * 1.5),
                    Life = 0.8,
                    Color = new ColorRgb(255, 60, 160)
                });
            }
            return;
        }

        // Yazı tuşları: Yem bırak
        if (key.Row >= 1 && key.Row <= 4 && _crumbs.Count < 10)
        {
            _crumbs.Add(new FoodCrumb
            {
                X = key.X + (key.Width * 0.5),
                Y = key.Y + (key.Height * 0.5),
                Offset = key.Offset,
                Life = 8.0
            });
        }
    }
}
