using System;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

/// <summary>
/// 🦠 Conway'in Yaşam Oyunu (Conway's Game of Life)
/// Klavye matrisi üzerinde hücresel otomat; yaşa göre renk değiştiren canlı hücreler.
/// </summary>
public sealed class GameOfLifeEffect : EffectBase
{
    public override string Id => "game_of_life";
    public override string DisplayName => "Yaşam Oyunu (Conway)";

    private const int COLS = 22;
    private const int ROWS = 6;

    private readonly int[,] _grid = new int[COLS, ROWS];
    private readonly int[,] _nextGrid = new int[COLS, ROWS];
    private readonly float[,] _fade = new float[COLS, ROWS];

    private double _stepTimer;
    private int _stagnationCounter;
    private int _lastLivingCount;
    private readonly Random _rng = new();

    public double StepInterval { get; set; } = 0.22;

    public override void Reset()
    {
        Array.Clear(_grid, 0, _grid.Length);
        Array.Clear(_nextGrid, 0, _nextGrid.Length);
        Array.Clear(_fade, 0, _fade.Length);
        _stepTimer = 0;
        _stagnationCounter = 0;
        SeedLife();
    }

    private void SeedLife()
    {
        Array.Clear(_grid, 0, _grid.Length);

        int startX = _rng.Next(3, COLS - 6);
        int startY = _rng.Next(1, ROWS - 3);

        _grid[startX + 1, startY] = 1;
        _grid[startX + 2, startY + 1] = 1;
        _grid[startX, startY + 2] = 1;
        _grid[startX + 1, startY + 2] = 1;
        _grid[startX + 2, startY + 2] = 1;

        int startX2 = (startX + 10) % (COLS - 3);
        _grid[startX2, 2] = 1;
        _grid[startX2 + 1, 2] = 1;
        _grid[startX2 + 2, 2] = 1;
    }

    public override void Update(double dt)
    {
        _stepTimer += dt;

        for (int x = 0; x < COLS; x++)
        {
            for (int y = 0; y < ROWS; y++)
            {
                if (_fade[x, y] > 0)
                {
                    _fade[x, y] = Math.Max(0.0f, _fade[x, y] - (float)(dt * 2.0));
                }
            }
        }

        if (_stepTimer >= StepInterval)
        {
            _stepTimer = 0;
            StepSimulation();
        }
    }

    private void StepSimulation()
    {
        int livingCount = 0;

        for (int x = 0; x < COLS; x++)
        {
            for (int y = 0; y < ROWS; y++)
            {
                int neighbors = CountNeighbors(x, y);
                bool isAlive = _grid[x, y] > 0;

                if (isAlive)
                {
                    if (neighbors == 2 || neighbors == 3)
                    {
                        _nextGrid[x, y] = _grid[x, y] + 1;
                        livingCount++;
                    }
                    else
                    {
                        _nextGrid[x, y] = 0;
                        _fade[x, y] = 1.0f;
                    }
                }
                else
                {
                    if (neighbors == 3)
                    {
                        _nextGrid[x, y] = 1;
                        livingCount++;
                    }
                    else
                    {
                        _nextGrid[x, y] = 0;
                    }
                }
            }
        }

        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                _grid[x, y] = _nextGrid[x, y];

        if (livingCount == _lastLivingCount || livingCount < 4)
        {
            _stagnationCounter++;
            if (_stagnationCounter > 7)
            {
                _stagnationCounter = 0;
                SeedLife();
            }
        }
        else
        {
            _stagnationCounter = 0;
        }
        _lastLivingCount = livingCount;
    }

    private int CountNeighbors(int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = (x + dx + COLS) % COLS;
                int ny = (y + dy + ROWS) % ROWS;
                if (_grid[nx, ny] > 0) count++;
            }
        }
        return count;
    }

    public override void Render(FrameBuffer buffer)
    {
        foreach (var key in KeyMapTable.AllKeys)
        {
            int cx = Math.Clamp(key.Col, 0, COLS - 1);
            int cy = Math.Clamp(key.Row, 0, ROWS - 1);

            int age = _grid[cx, cy];
            float fade = _fade[cx, cy];

            if (age > 0)
            {
                ColorRgb cellColor = age switch
                {
                    1 => new ColorRgb(0, 240, 255),
                    2 or 3 or 4 => new ColorRgb(34, 197, 94),
                    _ => new ColorRgb(245, 158, 11)
                };
                buffer.SetKey(key.Offset, cellColor);
            }
            else if (fade > 0.05f)
            {
                byte r = (byte)(fade * 40);
                byte g = (byte)(fade * 20);
                byte b = (byte)(fade * 120);
                buffer.SetKey(key.Offset, new ColorRgb(r, g, b));
            }
            else
            {
                buffer.SetKey(key.Offset, ColorRgb.Black);
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (isDown)
        {
            int cx = Math.Clamp(key.Col, 0, COLS - 1);
            int cy = Math.Clamp(key.Row, 0, ROWS - 1);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = (cx + dx + COLS) % COLS;
                    int ny = (cy + dy + ROWS) % ROWS;
                    _grid[nx, ny] = 1;
                }
            }
        }
    }
}
