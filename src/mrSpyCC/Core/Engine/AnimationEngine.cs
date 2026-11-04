using System;
using System.Diagnostics;
using System.Threading;
using mrSpyCC.Core.Device;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;

namespace mrSpyCC.Core.Engine;

public sealed class AnimationEngine : IDisposable
{
    private const double TARGET_FPS = 31.25; // 32.0 ms per frame — hardware safe ceiling for ITE8291
    private const double TARGET_FRAME_TIME_MS = 1000.0 / TARGET_FPS;

    private readonly IKeyboardDevice _device;
    private readonly LowLevelKeyboardHook _keyboardHook;
    private readonly FrameBuffer _frameBuffer = new();
    private readonly object _effectLock = new();

    public GlobalColorProcessor ColorProcessor { get; } = new();

    private Thread? _renderThread;
    private volatile bool _isRunning;
    private IEffect? _activeEffect;

    public event Action<FrameBuffer>? FrameRendered;
    public event Action<double>? FpsUpdated;

    public bool IsRunning => _isRunning;
    public IEffect? ActiveEffect => _activeEffect;

    public AnimationEngine(IKeyboardDevice device, LowLevelKeyboardHook keyboardHook)
    {
        _device = device;
        _keyboardHook = keyboardHook;
        _keyboardHook.KeyActivity += OnKeyActivity;
    }

    public void SetEffect(IEffect? effect)
    {
        lock (_effectLock)
        {
            _activeEffect = effect;
            _activeEffect?.Reset();
        }
    }

    public void ResetActiveEffect()
    {
        lock (_effectLock)
        {
            _activeEffect?.Reset();
            ColorProcessor.ResetPhase();
        }
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _keyboardHook.Start();

        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "mrSpyCC_RenderThread",
            Priority = ThreadPriority.AboveNormal
        };
        _renderThread.Start();
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _renderThread?.Join(1000);
        _renderThread = null;

        _keyboardHook.Stop();

        // Send a blank frame to cleanly turn off LEDs
        _frameBuffer.Clear();
        _device.SendFrame(_frameBuffer.Span);
    }

    private void RenderLoop()
    {
        NativeHid.TimeBeginPeriod(1);
        var sw = Stopwatch.StartNew();
        var fpsSw = Stopwatch.StartNew();
        int frameCount = 0;
        double lastTime = sw.Elapsed.TotalSeconds;

        try
        {
            while (_isRunning)
            {
                double frameStart = sw.Elapsed.TotalMilliseconds;
                double currentTime = sw.Elapsed.TotalSeconds;
                double dt = currentTime - lastTime;
                lastTime = currentTime;

                // 1. Update and Render Active Effect + Global Color/RGB Studio Processor
                lock (_effectLock)
                {
                    _frameBuffer.Clear();
                    if (_activeEffect != null)
                    {
                        _activeEffect.Update(dt);
                        _activeEffect.Render(_frameBuffer);
                        ColorProcessor.ProcessFrame(_frameBuffer, _activeEffect, dt);
                    }
                }

                // 2. Transmit frame to ITE8291 controller
                _device.SendFrame(_frameBuffer.Span);

                // 3. Notify UI listeners (Virtual Keyboard vector control)
                try
                {
                    FrameRendered?.Invoke(_frameBuffer);
                }
                catch { }

                frameCount++;
                if (fpsSw.Elapsed.TotalSeconds >= 1.0)
                {
                    double actualFps = frameCount / fpsSw.Elapsed.TotalSeconds;
                    FpsUpdated?.Invoke(actualFps);
                    frameCount = 0;
                    fpsSw.Restart();
                }

                // 4. Precision Frame Pacing
                double frameDuration = sw.Elapsed.TotalMilliseconds - frameStart;
                double sleepNeeded = TARGET_FRAME_TIME_MS - frameDuration;

                if (sleepNeeded > 1.5)
                {
                    Thread.Sleep((int)(sleepNeeded - 1.0));
                }

                // Fine spin-wait for the sub-millisecond tail
                while (sw.Elapsed.TotalMilliseconds - frameStart < TARGET_FRAME_TIME_MS)
                {
                    Thread.SpinWait(10);
                }
            }
        }
        finally
        {
            NativeHid.TimeEndPeriod(1);
        }
    }

    public void OnKeyActivity(KeyEntry key, bool isDown)
    {
        lock (_effectLock)
        {
            _activeEffect?.OnKeyActivity(key, isDown);
        }
    }

    public void Dispose()
    {
        Stop();
        _keyboardHook.KeyActivity -= OnKeyActivity;
    }
}
