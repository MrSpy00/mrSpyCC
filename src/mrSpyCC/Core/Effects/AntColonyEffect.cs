using System;
using System.Collections.Generic;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class AntColonyEffect : EffectBase
{
    public override string Id => "ant_colony";
    public override string DisplayName => "Karınca Kolonisi (Swarm Life)";

    private sealed class Ant
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public bool HasFood = false;
        public int NestIndex; // 0..3 (4 köşe yuva)
    }

    private sealed class FoodCrumb
    {
        public double X;
        public double Y;
        public int Offset;
        public int Amount = 4; // Parça sayısı
    }

    // 4 Köşe Yuva Koordinatları
    private static readonly (double X, double Y)[] Nests =
    [
        (0.5, 0.5),   // Sol Üst (Esc)
        (19.0, 0.5),  // Sağ Üst (Numpad -)
        (0.5, 5.0),   // Sol Alt (Ctrl)
        (19.0, 5.0)   // Sağ Alt (Numpad Enter)
    ];

    private readonly List<Ant> _ants = new();
    private readonly List<FoodCrumb> _crumbs = new();
    private double _time = 0;
    private static readonly Random _rng = new();

    public override void Reset()
    {
        _ants.Clear();
        _crumbs.Clear();
        _time = 0;

        // 20 Karınca Başlat
        for (int i = 0; i < 20; i++)
        {
            int nest = i % Nests.Length;
            _ants.Add(new Ant
            {
                X = Nests[nest].X,
                Y = Nests[nest].Y,
                NestIndex = nest,
                Vx = (_rng.NextDouble() - 0.5) * 4.0,
                Vy = (_rng.NextDouble() - 0.5) * 3.0
            });
        }
    }

    public override void Update(double dt)
    {
        _time += dt;

        foreach (var ant in _ants)
        {
            if (ant.HasFood)
            {
                // Besini yuvaya taşı!
                var nest = Nests[ant.NestIndex];
                double dx = nest.X - ant.X;
                double dy = nest.Y - ant.Y;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));

                if (dist < 0.6)
                {
                    // Yuvaya teslim edildi!
                    ant.HasFood = false;
                    ant.Vx = (_rng.NextDouble() - 0.5) * 4.0;
                    ant.Vy = (_rng.NextDouble() - 0.5) * 3.0;
                }
                else
                {
                    double spd = 5.5 * dt;
                    ant.X += (dx / dist) * Math.Min(dist, spd);
                    ant.Y += (dy / dist) * Math.Min(dist, spd);
                }
            }
            else
            {
                // Yem ara! En yakın besin kırıntısını kokla
                FoodCrumb? nearest = null;
                double minDistSq = double.MaxValue;

                foreach (var c in _crumbs)
                {
                    if (c.Amount <= 0) continue;
                    double d = Math.Pow(c.X - ant.X, 2) + Math.Pow(c.Y - ant.Y, 2);
                    if (d < minDistSq)
                    {
                        minDistSq = d;
                        nearest = c;
                    }
                }

                if (nearest != null && minDistSq < 25.0) // 5 birim koku menzili
                {
                    double dx = nearest.X - ant.X;
                    double dy = nearest.Y - ant.Y;
                    double dist = Math.Sqrt(minDistSq);

                    if (dist < 0.6)
                    {
                        // Besinden parça kopar!
                        nearest.Amount--;
                        ant.HasFood = true;
                    }
                    else
                    {
                        double spd = 6.0 * dt;
                        ant.X += (dx / dist) * Math.Min(dist, spd);
                        ant.Y += (dy / dist) * Math.Min(dist, spd);
                    }
                }
                else
                {
                    // Başıboş dolaş
                    ant.X += ant.Vx * dt;
                    ant.Y += ant.Vy * dt;

                    // Kenarlardan sekme
                    if (ant.X < 0.5 || ant.X > 19.5) ant.Vx = -ant.Vx;
                    if (ant.Y < 0.5 || ant.Y > 5.2) ant.Vy = -ant.Vy;

                    // Rastgele yön değişimi
                    if (_rng.NextDouble() < 0.05)
                    {
                        ant.Vx = (_rng.NextDouble() - 0.5) * 4.0;
                        ant.Vy = (_rng.NextDouble() - 0.5) * 3.0;
                    }
                }
            }
        }

        // Boşalan kırıntıları temizle
        for (int i = _crumbs.Count - 1; i >= 0; i--)
        {
            if (_crumbs[i].Amount <= 0)
                _crumbs.RemoveAt(i);
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        // Koyu toprak fonu
        buffer.Fill(4, 5, 8);

        // 1. Yuvalar (4 Köşede zümrüt ışıklar)
        foreach (var nest in Nests)
        {
            var key = KeyMapTable.FindNearest(nest.X, nest.Y);
            if (key != null)
            {
                buffer.SetColor(key.Offset, 0, 180, 100);
            }
        }

        // 2. Besin Kırıntıları (Altın Sarısı parıltı)
        foreach (var c in _crumbs)
        {
            var key = KeyMapTable.FindByOffset(c.Offset);
            if (key != null)
            {
                byte g = (byte)Math.Clamp(c.Amount * 60, 50, 255);
                buffer.SetColor(key.Offset, 255, g, 0);
            }
        }

        // 3. Karıncalar (Besin taşıyanlar altın, boş gezenler neon camgöbeği)
        foreach (var ant in _ants)
        {
            var key = KeyMapTable.FindNearest(ant.X, ant.Y);
            if (key != null)
            {
                if (ant.HasFood)
                    buffer.SetColor(key.Offset, 255, 215, 0); // Besin Taşıyan
                else
                    buffer.SetColor(key.Offset, 0, 240, 255); // Arayan Karınca
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            // Kullanıcının bastığı her tuşa tatlı besin kırıntısı bırak!
            if (key.Row >= 1 && key.Row <= 4 && _crumbs.Count < 12)
            {
                _crumbs.Add(new FoodCrumb
                {
                    X = key.X + (key.Width * 0.5),
                    Y = key.Y + (key.Height * 0.5),
                    Offset = key.Offset,
                    Amount = 5
                });
            }
        }
    }
}
