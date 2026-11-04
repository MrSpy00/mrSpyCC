using System;
using System.Diagnostics;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Effects;

public sealed class ReactionTestEffect : EffectBase
{
    public override string Id => "reaction_test";
    public override string DisplayName => "Tepki Hız Sınavı (Reaction Test)";

    private enum State
    {
        WaitingForSignal,
        SignalActive,
        Success,
        FalseStart
    }

    private const double AUTO_RESTART_TIME = 3.0;

    private State _state = State.WaitingForSignal;
    private double _stateTimer = 0;
    private double _waitDuration = 2.5;

    private KeyEntry? _targetKey;
    private readonly Stopwatch _reactionStopwatch = new();
    private double _lastReactionMs = 0;
    private double _bestReactionMs = double.MaxValue;
    private double _time = 0;

    private static readonly Random _rng = new();

    public override void Reset()
    {
        _state = State.WaitingForSignal;
        _stateTimer = 0;
        _waitDuration = 1.5 + (_rng.NextDouble() * 3.0); // 1.5 to 4.5s random suspense
        _targetKey = null;
        _reactionStopwatch.Reset();
        _lastReactionMs = 0;
        _time = 0;
    }

    public override void Update(double dt)
    {
        _time += dt;
        _stateTimer += dt;

        switch (_state)
        {
            case State.WaitingForSignal:
                if (_stateTimer >= _waitDuration)
                {
                    // Pick a random letter or number key
                    var validKeys = KeyMapTable.AllKeys;
                    do
                    {
                        _targetKey = validKeys[_rng.Next(validKeys.Count)];
                    }
                    while (_targetKey.Row < 1 || _targetKey.Row > 4 || _targetKey.VirtualKey == 0);

                    _state = State.SignalActive;
                    _stateTimer = 0;
                    _reactionStopwatch.Restart();
                }
                break;

            case State.SignalActive:
                // Auto timeout after 2.5 seconds of no reaction
                if (_reactionStopwatch.ElapsedMilliseconds > 2500)
                {
                    _state = State.FalseStart;
                    _stateTimer = 0;
                    _reactionStopwatch.Stop();
                }
                break;

            case State.Success:
            case State.FalseStart:
                // 3 Saniye kuralı: 3 saniye sonra otomatik yeni tur başlar
                if (_stateTimer >= AUTO_RESTART_TIME)
                {
                    Reset();
                }
                break;
        }
    }

    public override void Render(FrameBuffer buffer)
    {
        switch (_state)
        {
            case State.WaitingForSignal:
                // Nefes alan mor ambiyans (Bekleme gerilimi)
                double breath = Math.Sin(_time * 4.0) * 0.5 + 0.5;
                byte br = (byte)(10 + (breath * 15));
                byte bb = (byte)(25 + (breath * 35));
                buffer.Fill(br, 4, bb);
                break;

            case State.SignalActive:
                // Hedef tuş KOR KIRMIZI / PARLAK BEYAZ yanar
                buffer.Fill(6, 4, 10);
                if (_targetKey != null)
                {
                    buffer.SetColor(_targetKey.Offset, 255, 0, 45);
                }
                break;

            case State.Success:
                // Başarı yeşil şok dalgası
                double progress = Math.Min(1.0, _stateTimer / AUTO_RESTART_TIME);
                byte waveG = (byte)((1.0 - progress) * 200);
                buffer.Fill(4, waveG, 25);

                if (_targetKey != null)
                    buffer.SetColor(_targetKey.Offset, 255, 255, 255);

                // F1-F12 Hız Göstergesi
                RenderSpeedMeter(buffer);
                RenderCountdownBar(buffer);
                break;

            case State.FalseStart:
                // Hatalı tuş kırmızı flaşı
                bool flash = ((int)(_stateTimer * 8.0)) % 2 == 0;
                if (flash) buffer.Fill(180, 10, 10);
                else buffer.Fill(35, 4, 4);

                RenderCountdownBar(buffer);
                break;
        }
    }

    private void RenderCountdownBar(FrameBuffer buffer)
    {
        // Spacebar üzerinde veya alt satırda geri sayım
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        double remainingRatio = Math.Clamp(1.0 - (_stateTimer / AUTO_RESTART_TIME), 0.0, 1.0);
        int remainingBars = (int)Math.Ceiling(remainingRatio * fOffsets.Length);

        // F tuşlarının altında küçük geri sayım
        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i >= remainingBars)
            {
                // Sönen barlar
                buffer.BlendPixel(fOffsets[i], 0, 0, 0, 180);
            }
        }
    }

    private void RenderSpeedMeter(FrameBuffer buffer)
    {
        int[] fOffsets = [45, 69, 93, 117, 141, 165, 189, 213, 237, 261, 285, 309];
        // 160ms = 12 LEDs (Efsanevi / S-Tier), 450ms = 1 LED
        int lit = Math.Clamp((int)((450.0 - _lastReactionMs) / 25.0), 1, fOffsets.Length);

        for (int i = 0; i < fOffsets.Length; i++)
        {
            if (i < lit)
            {
                if (lit >= 10) buffer.SetColor(fOffsets[i], 180, 0, 255);    // Mor / Cyberpunk Efsanevi
                else if (lit >= 7) buffer.SetColor(fOffsets[i], 0, 255, 120); // Zümrüt Yeşili
                else if (lit >= 4) buffer.SetColor(fOffsets[i], 255, 215, 0); // Altın Sarısı
                else buffer.SetColor(fOffsets[i], 255, 100, 0);               // Turuncu
            }
            else
            {
                buffer.SetColor(fOffsets[i], 15, 20, 30);
            }
        }
    }

    public override void OnKeyActivity(KeyEntry key, bool isDown)
    {
        if (!isDown) return;

        if (_state == State.Success || _state == State.FalseStart)
        {
            if (_stateTimer > 0.3)
            {
                Reset(); // Anında yeni tur başlat
            }
            return;
        }

        if (_state == State.WaitingForSignal)
        {
            // Erken basış (False Start)
            _state = State.FalseStart;
            _stateTimer = 0;
        }
        else if (_state == State.SignalActive)
        {
            if (key.Offset == _targetKey?.Offset || key.VirtualKey == _targetKey?.VirtualKey)
            {
                // İsabet!
                _reactionStopwatch.Stop();
                _lastReactionMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
                if (_lastReactionMs < _bestReactionMs)
                    _bestReactionMs = _lastReactionMs;

                _state = State.Success;
                _stateTimer = 0;
            }
            else
            {
                // Yanlış tuş!
                _reactionStopwatch.Stop();
                _state = State.FalseStart;
                _stateTimer = 0;
            }
        }
    }
}
