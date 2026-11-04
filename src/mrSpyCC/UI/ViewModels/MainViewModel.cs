using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using mrSpyCC.Core.Audio;
using mrSpyCC.Core.Config;
using mrSpyCC.Core.Device;
using mrSpyCC.Core.Effects;
using mrSpyCC.Core.Engine;
using mrSpyCC.Core.Input;
using mrSpyCC.Core.KeyMatrix;
using mrSpyCC.Core.VendorLock;

namespace mrSpyCC.UI.ViewModels;

public sealed class EffectItemViewModel : INotifyPropertyChanged
{
    private static readonly System.Windows.Media.SolidColorBrush GoldBrush = new(System.Windows.Media.Color.FromRgb(251, 191, 36));
    private static readonly System.Windows.Media.SolidColorBrush MutedBrush = new(System.Windows.Media.Color.FromRgb(71, 85, 105));

    static EffectItemViewModel()
    {
        GoldBrush.Freeze();
        MutedBrush.Freeze();
    }

    private bool _isFavorite;

    public IEffect Effect { get; }
    public string Id => Effect.Id;
    public string DisplayName => Effect.DisplayName;
    public string Category { get; }
    public string Description { get; }
    public string IconGlyph { get; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StarGlyph));
                OnPropertyChanged(nameof(StarBrush));
                OnPropertyChanged(nameof(StarToolTip));
            }
        }
    }

    public string StarGlyph => _isFavorite ? "★" : "☆";
    public System.Windows.Media.SolidColorBrush StarBrush => _isFavorite ? GoldBrush : MutedBrush;
    public string StarToolTip => _isFavorite ? "Favorilerden Çıkar" : "Favorilere Ekle";

    public EffectItemViewModel(IEffect effect, string category, string description, string iconGlyph, bool isFavorite = false)
    {
        Effect = effect;
        Category = category;
        Description = description;
        IconGlyph = iconGlyph;
        _isFavorite = isFavorite;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Ite8291Device _device;
    private readonly LowLevelKeyboardHook _hook;
    private readonly WasapiAudioCapture _audioCapture;
    private readonly AnimationEngine _engine;
    private readonly System.Windows.Threading.DispatcherTimer _saveDebounceTimer;
    private readonly Dictionary<string, EffectColorSettings> _perEffectColors = new(StringComparer.OrdinalIgnoreCase);
    private AppConfig _config;

    private string _connectionStatusText = "Donanım Aranıyor...";
    private string _lockStatusText = "Kilit Durumu: Bilinmiyor";
    private string _fpsText = "33.0 FPS";
    private string _wpmText = "0 WPM";
    private int _lastFpsTenths = -1;
    private int _lastWpmValue = -1;
    private int _lastTotalKeystrokes = -1;
    private bool _isExclusiveLocked;
    private bool _isElevated;
    private int _brightness = 100;
    private int _selectedCategoryIndex = 0;
    private double _genericSpeedMultiplier = 1.0;
    private IEffect? _activeEffect;
    private EffectItemViewModel? _selectedEffectItem;

    public event Action? ProfileOrEffectStateRestored;

    // 1. Temel / Klasik Efektler
    public StaticEffect Static { get; } = new();
    public BreathingEffect Breathing { get; } = new();
    public SpectrumCycleEffect SpectrumCycle { get; } = new();
    public ColorCycleEffect ColorCycle { get; } = new();

    // 2. Dalga / Akış Efektleri
    public WaveEffect Wave { get; } = new();
    public RainbowWaveEffect RainbowWave { get; } = new();
    public RippleEffect Ripple { get; } = new();
    public MarqueeEffect Marquee { get; } = new();
    public PinwheelEffect Pinwheel { get; } = new();
    public ChevronEffect Chevron { get; } = new();

    // 3. Reactive (Tuşa Duyarlı) Efektler
    public ReactiveEffect Reactive { get; } = new();
    public TypingHeatmapEffect TypingHeatmap { get; } = new();
    public AuroraEffect Aurora { get; } = new();

    // 4. Doğa / Elemental Efektler
    public FlameEffect Flame { get; } = new();
    public RainEffect Rain { get; } = new();
    public LightningEffect Lightning { get; } = new();
    public SparkleEffect Sparkle { get; } = new();
    public MatrixEffect Matrix { get; } = new();
    public DigitalRainEffect DigitalRain { get; } = new();

    // 5. Özel / Gelişmiş Efektler
    public WheelEffect Wheel { get; } = new();
    public FireworksEffect Fireworks { get; } = new();
    public PartyEffect Party { get; } = new();
    public ScanEffect Scan { get; } = new();
    public PulseEffect Pulse { get; } = new();
    public StrobeEffect Strobe { get; } = new();
    public JellybeanEffect Jellybean { get; } = new();

    // 6. Yardımcı / Statik Modlar
    public OffEffect Off { get; } = new();
    public GamingModeEffect GamingMode { get; } = new();
    public StaticPerKeyEffect StaticPainter { get; } = new();
    public AudioVisualizerEffect Visualizer { get; }

    // 7. Egzotik & Bilimsel İleri Düzey Efektler
    public FluidEffect Fluid { get; } = new();
    public PlasmaEffect Plasma { get; } = new();
    public BlackHoleEffect BlackHole { get; } = new();
    public PerlinFogEffect PerlinFog { get; } = new();
    public GlitchEffect Glitch { get; } = new();
    public GameOfLifeEffect GameOfLife { get; } = new();
    public DnaHelixEffect DnaHelix { get; } = new();
    public SystemPulseEffect SystemPulse { get; } = new();
    public AudioSplitEffect AudioSplit { get; }

    // 8. Canlı Varlıklar (Living Beings & AI Companions)
    public LivingEyesEffect LivingEyes { get; } = new();
    public VirtualPetEffect VirtualPet { get; } = new();
    public KeyboardBuddyEffect KeyboardBuddy { get; } = new();
    public MoodCreatureEffect MoodCreature { get; }
    public CyberOwlEffect CyberOwl { get; } = new();
    public AntColonyEffect AntColony { get; } = new();

    // 9. Klavyede Oynanabilir Mini Oyunlar & İnteraktif Modlar
    public DinoRunEffect DinoRun { get; } = new();
    public SnakeEffect Snake { get; } = new();
    public SpaceInvadersEffect SpaceInvaders { get; } = new();
    public PongEffect Pong { get; } = new();
    public ReactionTestEffect ReactionTest { get; } = new();
    public TypingRainEffect TypingRain { get; } = new();
    public CursorGhostEffect CursorGhost { get; } = new();
    public BreakoutEffect Breakout { get; } = new();
    public FlappyBirdEffect FlappyBird { get; } = new();
    public WhackAMoleEffect WhackAMole { get; } = new();

    public ObservableCollection<EffectItemViewModel> AllEffects { get; } = new();
    public ObservableCollection<EffectItemViewModel> FilteredEffects { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<ProfileModel> Profiles { get; } = new();

    public Ite8291Device Device => _device;
    public AnimationEngine Engine => _engine;
    public GlobalColorProcessor ColorProcessor => _engine.ColorProcessor;
    public IEffect? ActiveEffect => _activeEffect;

    // Universal Global Color, Dual-Color, Palette & RGB Engine Properties
    public EffectColorMode ActiveColorMode
    {
        get => _engine.ColorProcessor.Mode;
        set
        {
            if (_engine.ColorProcessor.Mode != value)
            {
                _engine.ColorProcessor.Mode = value;
                SyncNativeEffectColors();
                SaveActiveEffectColorSettings();
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public ColorRgb GlobalPrimaryColor
    {
        get => _engine.ColorProcessor.PrimaryColor;
        set
        {
            _engine.ColorProcessor.PrimaryColor = value;
            SyncNativeEffectColors();
            SaveActiveEffectColorSettings();
            SaveCurrentState();
            OnPropertyChanged();
        }
    }

    public ColorRgb GlobalSecondaryColor
    {
        get => _engine.ColorProcessor.SecondaryColor;
        set
        {
            _engine.ColorProcessor.SecondaryColor = value;
            SyncNativeEffectColors();
            SaveActiveEffectColorSettings();
            SaveCurrentState();
            OnPropertyChanged();
        }
    }

    public DualColorBlendMode ActiveDualBlendMode
    {
        get => _engine.ColorProcessor.DualBlendMode;
        set
        {
            if (_engine.ColorProcessor.DualBlendMode != value)
            {
                _engine.ColorProcessor.DualBlendMode = value;
                SaveActiveEffectColorSettings();
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public RgbFlowStyle ActiveRgbStyle
    {
        get => _engine.ColorProcessor.RgbStyle;
        set
        {
            if (_engine.ColorProcessor.RgbStyle != value)
            {
                _engine.ColorProcessor.RgbStyle = value;
                SaveActiveEffectColorSettings();
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public int ActiveStudioPaletteIndex
    {
        get => _engine.ColorProcessor.StudioPaletteIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, GlobalColorProcessor.StudioPalettes.Length - 1);
            if (_engine.ColorProcessor.StudioPaletteIndex != clamped)
            {
                _engine.ColorProcessor.StudioPaletteIndex = clamped;
                SaveActiveEffectColorSettings();
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public double GlobalRgbSpeed
    {
        get => _engine.ColorProcessor.RgbSpeed;
        set
        {
            double clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(_engine.ColorProcessor.RgbSpeed - clamped) > 0.005)
            {
                _engine.ColorProcessor.RgbSpeed = clamped;
                SaveActiveEffectColorSettings();
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public void SwapDualColors()
    {
        ColorRgb p = _engine.ColorProcessor.PrimaryColor;
        ColorRgb s = _engine.ColorProcessor.SecondaryColor;
        _engine.ColorProcessor.PrimaryColor = s;
        _engine.ColorProcessor.SecondaryColor = p;
        SyncNativeEffectColors();
        SaveActiveEffectColorSettings();
        SaveCurrentState();
        OnPropertyChanged(nameof(GlobalPrimaryColor));
        OnPropertyChanged(nameof(GlobalSecondaryColor));
        ProfileOrEffectStateRestored?.Invoke();
    }

    public void ApplyQuickDualPreset(ColorRgb primary, ColorRgb secondary)
    {
        _engine.ColorProcessor.PrimaryColor = primary;
        _engine.ColorProcessor.SecondaryColor = secondary;
        if (_activeEffect is StaticEffect && Static.Mode != StaticEffect.StaticLayoutMode.UniformColor)
        {
            // Keep Static 3-Zone / WASD mode using the 2 colors directly
            SyncNativeEffectColors();
        }
        else
        {
            _engine.ColorProcessor.Mode = EffectColorMode.DualColor;
            SyncNativeEffectColors();
        }
        SaveActiveEffectColorSettings();
        SaveCurrentState();
        OnPropertyChanged(nameof(ActiveColorMode));
        OnPropertyChanged(nameof(GlobalPrimaryColor));
        OnPropertyChanged(nameof(GlobalSecondaryColor));
        ProfileOrEffectStateRestored?.Invoke();
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        set => SetField(ref _connectionStatusText, value);
    }

    public string LockStatusText
    {
        get => _lockStatusText;
        set => SetField(ref _lockStatusText, value);
    }

    public string FpsText
    {
        get => _fpsText;
        set => SetField(ref _fpsText, value);
    }

    public string WpmText
    {
        get => _wpmText;
        set => SetField(ref _wpmText, value);
    }

    public bool IsExclusiveLocked
    {
        get => _isExclusiveLocked;
        set => SetField(ref _isExclusiveLocked, value);
    }

    public bool IsElevated
    {
        get => _isElevated;
        set => SetField(ref _isElevated, value);
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (SetField(ref _brightness, value))
            {
                _device.Brightness = value;
                SaveCurrentState();
            }
        }
    }

    public int SelectedCategoryIndex
    {
        get => _selectedCategoryIndex;
        set
        {
            if (SetField(ref _selectedCategoryIndex, value))
            {
                ApplyCategoryFilter();
            }
        }
    }

    public EffectItemViewModel? SelectedEffectItem
    {
        get => _selectedEffectItem;
        set
        {
            if (SetField(ref _selectedEffectItem, value) && value != null)
            {
                ApplyEffect(value.Effect);
            }
        }
    }

    public bool AutoStartEnabled
    {
        get => _config.AutoStartWithWindows;
        set
        {
            if (_config.AutoStartWithWindows != value)
            {
                _config.AutoStartWithWindows = value;
                OnPropertyChanged();
                if (value)
                    TaskSchedulerHelper.InstallAutoStartTask();
                else
                    TaskSchedulerHelper.RemoveAutoStartTask();
                ConfigManager.SaveConfig(_config);
            }
        }
    }

    public MainViewModel()
    {
        _config = ConfigManager.LoadConfig();
        _isElevated = VendorLockManager.IsElevated;

        // Otomatik başlatma kaydını (HKCU\...\Run + StartupApproved + TaskScheduler) arka planda doğrula ve onar
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            bool autoStart = _config.AutoStartWithWindows;
            TaskSchedulerHelper.SyncAndRepairAutoStart(ref autoStart);
            if (_config.AutoStartWithWindows != autoStart)
            {
                _config.AutoStartWithWindows = autoStart;
                ConfigManager.SaveConfig(_config);
                Application.Current?.Dispatcher?.InvokeAsync(() => OnPropertyChanged(nameof(AutoStartEnabled)));
            }
        });

        _device = new Ite8291Device();
        _hook = new LowLevelKeyboardHook();
        _hook.KeyActivity += (key, isDown) =>
        {
            if (isDown && _activeEffect is not TypingHeatmapEffect)
            {
                TypingHeatmap.RecordGlobalKeystroke();
            }
        };
        _audioCapture = new WasapiAudioCapture();
        _audioCapture.Start();

        Visualizer = new AudioVisualizerEffect(_audioCapture);
        AudioSplit = new AudioSplitEffect(_audioCapture);
        MoodCreature = new MoodCreatureEffect(_audioCapture);

        _engine = new AnimationEngine(_device, _hook);
        _engine.FpsUpdated += fps =>
        {
            var app = Application.Current;
            if (app == null) return;
            int tenths = (int)Math.Round(fps * 10.0);
            if (tenths == _lastFpsTenths) return;
            _lastFpsTenths = tenths;

            app.Dispatcher?.InvokeAsync(() =>
            {
                if (app.MainWindow?.IsVisible == true)
                {
                    FpsText = $"{tenths / 10.0:F1} FPS";
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        _saveDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveDebounceTimer.Tick += (s, e) =>
        {
            _saveDebounceTimer.Stop();
            DoSaveConfig();
        };

        RegisterCategories();
        RegisterAllEffects();
        LoadProfiles();

        // Start hardware connection and load active profile
        InitializeHardware();
    }

    private void RegisterCategories()
    {
        Categories.Add("Tümü");
        Categories.Add("⭐ Favoriler");
        Categories.Add("🐾 Canlı Varlıklar");
        Categories.Add("🎮 Mini Oyunlar");
        Categories.Add("Temel");
        Categories.Add("Dalga & Akış");
        Categories.Add("Reaktif");
        Categories.Add("Doğa & Elemental");
        Categories.Add("Özel & Gelişmiş");
        Categories.Add("Yardımcı & Oyun");
        Categories.Add("🧬 Egzotik & Bilim");
    }

    private void AddEffect(IEffect eff, string category, string description, string glyph)
    {
        bool isFav = _config.FavoriteEffectIds.Contains(eff.Id);
        AllEffects.Add(new EffectItemViewModel(eff, category, description, glyph, isFav));
    }

    private void RegisterAllEffects()
    {
        AllEffects.Clear();

        // 1. Canlı Varlıklar (Living Beings & AI Companions)
        AddEffect(LivingEyes, "🐾 Canlı Varlıklar", "Fareyi takip eden gözler; göz kırpar, yazarken odaklanır, uyur.", "👁️");
        AddEffect(VirtualPet, "🐾 Canlı Varlıklar", "Klavyede gezen evcil hayvan; tuşlarla beslenir, [Space] ile zıplar.", "🐉");
        AddEffect(KeyboardBuddy, "🐾 Canlı Varlıklar", "Bastığın tuşlara merakla koşan ve ışıltı saçan sevimli siber yoldaş.", "🤖");
        AddEffect(MoodCreature, "🐾 Canlı Varlıklar", "CPU yükü ve ses temposuna göre ruh hali ve formu değişen canlı.", "🦎");
        AddEffect(CyberOwl, "🐾 Canlı Varlıklar", "Üst satırda tüneyen bilge siber baykuş; fareyi izler, kanat çırpar.", "🦉");
        AddEffect(AntColony, "🐾 Canlı Varlıklar", "Klavyede besin arayan ve basılan tuşları yuvalarına taşıyan sürü zekası.", "🐜");

        // 2. Klavyede Oynanabilir Mini Oyunlar & İnteraktif Modlar
        AddEffect(DinoRun, "🎮 Mini Oyunlar", "[Space] veya [▲] ile zıpla, engellerden kaç! 3s auto-restart.", "🦖");
        AddEffect(Snake, "🎮 Mini Oyunlar", "[Ok Tuşları] veya [WASD] ile yönlendir, altın elma ve sindirim.", "🐍");
        AddEffect(SpaceInvaders, "🎮 Mini Oyunlar", "[◄/►] ile gemiyi sür, sığınakları koru, uzaylıları ve UFO'yu vur!", "🚀");
        AddEffect(Breakout, "🎮 Mini Oyunlar", "[◄/►] ile raketi kaydır, seken topla neon tuğlaları kır!", "🧱");
        AddEffect(FlappyBird, "🎮 Mini Oyunlar", "[Space] ile kanat çırp, yaklaşan borular arasından süzül!", "🐤");
        AddEffect(WhackAMole, "🎮 Mini Oyunlar", "Harflerde beliren köstebeklere hızla bas! Refleks arcade.", "🔨");
        AddEffect(Pong, "🎮 Mini Oyunlar", "[▲/▼] ile raketi kontrol et, topu AI rakibe karşı kaçırma!", "🏓");
        AddEffect(ReactionTest, "🎮 Mini Oyunlar", "Parlayan tuşa anında bas! Refleksini ve milisaniye hızını test et.", "🎯");
        AddEffect(TypingRain, "🎮 Mini Oyunlar", "Yazdığın her harften damlalar süzülür ve alt barda birikir.", "🌧️");
        AddEffect(CursorGhost, "🎮 Mini Oyunlar", "Fare imlecinin klavye izdüşümü ve arkasından süzülen neon kuyruk.", "👻");

        // 3. Temel
        AddEffect(Static, "Temel", "Sabit tek renk veya 3-bölge ayrık aydınlatma.", "⏹");
        AddEffect(Breathing, "Temel", "Yavaşça soluklaşıp parlayan yumuşak nefes döngüsü.", "🫁");
        AddEffect(SpectrumCycle, "Temel", "Tüm klavye boyunca kesintisiz gökkuşağı ton geçişi.", "🌈");
        AddEffect(ColorCycle, "Temel", "Kullanıcı paletindeki renkler arasında yumuşak geçiş.", "🔄");

        // 4. Dalga & Akış
        AddEffect(Wave, "Dalga & Akış", "Açı ve frekansı ayarlanabilir çoklu renk dalgası.", "🌊");
        AddEffect(RainbowWave, "Dalga & Akış", "Klasik kesintisiz pürüzsüz gökkuşağı dalgası.", "🌊");
        AddEffect(Ripple, "Dalga & Akış", "Merkezden dışa doğru yayılan hipnotik su dalgası.", "💧");
        AddEffect(Marquee, "Dalga & Akış", "Satır satır kıvrılarak akan kayar ışık şeridi.", "💫");
        AddEffect(Pinwheel, "Dalga & Akış", "Merkez etrafında dönen sarmal renk çarkı.", "🌀");
        AddEffect(Chevron, "Dalga & Akış", "V şeklinde klavyeyi tarayan çift yönlü dalga.", "⏩");

        // 5. Reaktif
        AddEffect(Reactive, "Reaktif", "Basılan tuş anında parlar, su dalgası veya lazer saçar.", "⚡");
        AddEffect(TypingHeatmap, "Reaktif", "Basılan tuşlar ısınır; canlı WPM ve istatistik üretir.", "🔥");
        AddEffect(Aurora, "Reaktif", "Kuzey ışıkları akışı ve basılan tuşlarda auroral parıltı.", "🌌");

        // 6. Doğa & Elemental
        AddEffect(Flame, "Doğa & Elemental", "Alt sıradan yükselen gerçekçi fiziksel alev kıvılcımları.", "🔥");
        AddEffect(Rain, "Doğa & Elemental", "Sütunlar boyunca süzülen yağmur damlaları ve su sıçraması.", "🌧");
        AddEffect(Lightning, "Doğa & Elemental", "Gök gürültülü fırtına; rastgele çakan şimşekler.", "⚡");
        AddEffect(Sparkle, "Doğa & Elemental", "Gece gökyüzünde rastgele parıldayan yıldızlar.", "✨");
        AddEffect(Matrix, "Doğa & Elemental", "Matrix tarzı akan yeşil, altın veya siber dijital kod.", "💻");
        AddEffect(DigitalRain, "Doğa & Elemental", "Tüm sütunları kaplayan ultra yoğun siber kod yağmuru.", "🟢");

        // 7. Özel & Gelişmiş
        AddEffect(Wheel, "Özel & Gelişmiş", "Klavyenin tam merkezinde dönen spektrum çarkı.", "🎡");
        AddEffect(Fireworks, "Özel & Gelişmiş", "Yükselen roketler ve klavye geneline yayılan havai fişekler.", "🎆");
        AddEffect(Party, "Özel & Gelişmiş", "BPM tempolu disko, ritmik rastgele renk sıçramaları.", "🎉");
        AddEffect(Scan, "Özel & Gelişmiş", "Knight Rider / Cylon tarzı sağa sola kayan lazer barı.", "🚨");
        AddEffect(Pulse, "Özel & Gelişmiş", "Senkron kalp atışı ve çift vuruşlu nabız animasyonu.", "💓");
        AddEffect(Strobe, "Özel & Gelişmiş", "Yüksek görünürlüklü stroboskopik flaş efekti.", "⚡");
        AddEffect(Jellybean, "Özel & Gelişmiş", "QMK stili pastel renkli canlı jellybean damlaları.", "🍬");

        // 8. Yardımcı & Oyun
        AddEffect(GamingMode, "Yardımcı & Oyun", "WASD, oklar ve makro tuşları vurgulanır; arka plan loşlaşır.", "🎮");
        AddEffect(Visualizer, "Yardımcı & Oyun", "Windows sistem sesine gerçek zamanlı FFT reaktif spektrum.", "🎵");
        AddEffect(StaticPainter, "Yardımcı & Oyun", "Tuş tuş özel boyama yapabileceğiniz interaktif harita.", "🎨");
        AddEffect(Off, "Yardımcı & Oyun", "Tüm klavye aydınlatmasını tamamen kapatır.", "🔌");

        // 9. Egzotik & Bilim (Yeni Nesil İleri Düzey)
        AddEffect(Fluid, "🧬 Egzotik & Bilim", "Akışkanlar mekaniği, viskozite ve dalga simülasyonu.", "🌊");
        AddEffect(Plasma, "🧬 Egzotik & Bilim", "Çok frekanslı sürekli organik lav lambası plazma alanı.", "🔮");
        AddEffect(BlackHole, "🧬 Egzotik & Bilim", "Kütleçekimsel tekillik çekimi, olay ufku ve süpernova patlaması.", "🕳️");
        AddEffect(PerlinFog, "🧬 Egzotik & Bilim", "2D Perlin gürültüsüyle süzülen rüya gibi hacimsel sis.", "🌫️");
        AddEffect(Glitch, "🧬 Egzotik & Bilim", "CRT elektron taraması, RGB kromatik sapma ve dijital bitflip.", "👾");
        AddEffect(GameOfLife, "🧬 Egzotik & Bilim", "Conway'in hücresel yaşam simülasyonu; yaşlandıkça renk evrilir.", "🦠");
        AddEffect(DnaHelix, "🧬 Egzotik & Bilim", "3D dönen çift sarmal iplikçikler ve nükleotit baz bağları.", "🧬");
        AddEffect(SystemPulse, "🧬 Egzotik & Bilim", "Win32 telemetrisiyle gerçek CPU yüküne senkronize canlı nabız.", "💓");
        AddEffect(AudioSplit, "🧬 Egzotik & Bilim", "Bas alt satırda, vokaller ortada, tiz F tuşlarında 3-bant ses.", "🎛️");

        ApplyCategoryFilter();
    }

    private void ApplyCategoryFilter()
    {
        FilteredEffects.Clear();
        string cat = Categories.Count > _selectedCategoryIndex ? Categories[_selectedCategoryIndex] : "Tümü";

        foreach (var eff in AllEffects)
        {
            if (cat == "Tümü")
            {
                FilteredEffects.Add(eff);
            }
            else if (cat == "⭐ Favoriler")
            {
                if (eff.IsFavorite)
                {
                    FilteredEffects.Add(eff);
                }
            }
            else if (eff.Category == cat)
            {
                FilteredEffects.Add(eff);
            }
        }

        if (FilteredEffects.Count > 0 && (SelectedEffectItem == null || !FilteredEffects.Contains(SelectedEffectItem)))
        {
            SelectedEffectItem = FilteredEffects[0];
        }
    }

    public void ToggleFavorite(string effectId)
    {
        var item = AllEffects.FirstOrDefault(e => e.Id.Equals(effectId, StringComparison.OrdinalIgnoreCase));
        if (item == null) return;

        item.IsFavorite = !item.IsFavorite;

        if (item.IsFavorite)
        {
            if (!_config.FavoriteEffectIds.Contains(item.Id))
                _config.FavoriteEffectIds.Add(item.Id);
        }
        else
        {
            _config.FavoriteEffectIds.Remove(item.Id);
        }

        SaveCurrentState();

        string currentCat = Categories.Count > _selectedCategoryIndex ? Categories[_selectedCategoryIndex] : "Tümü";
        if (currentCat == "⭐ Favoriler")
        {
            ApplyCategoryFilter();
        }
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var p in _config.Profiles)
        {
            Profiles.Add(p);
        }
    }

    public void InitializeHardware()
    {
        ApplyProfile(_config.ActiveProfile);
        _device.Brightness = _brightness;
        _engine.Start();

        ConnectionStatusText = "Donanım Kilidi Alınıyor...";
        LockStatusText = "GCUBridge İzole Ediliyor...";

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var status = VendorLockManager.AcquireLock(_device, allowSharedFallback: true);
            _device.Brightness = _brightness;
            Application.Current?.Dispatcher?.InvokeAsync(() => UpdateStatus(status));
        });
    }

    public void RequestExclusiveLock()
    {
        if (!_isElevated)
        {
            VendorLockManager.RelaunchElevated();
            Application.Current.Shutdown();
            return;
        }

        ConnectionStatusText = "Donanım Kilidi Yenileniyor...";
        LockStatusText = "GCUBridge İzole Ediliyor...";

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var status = VendorLockManager.AcquireLock(_device, allowSharedFallback: false);
            _device.Brightness = _brightness;
            Application.Current?.Dispatcher?.InvokeAsync(() => UpdateStatus(status));
        });
    }

    private void UpdateStatus(LockStatus status)
    {
        if (_device.IsConnected)
        {
            ConnectionStatusText = "Bağlandı (ITE8291 VID:048D PID:600B)";
            if (_device.IsExclusiveLockHeld)
            {
                IsExclusiveLocked = true;
                LockStatusText = "Özel Kilit AKTİF (GCUBridge izole edildi)";
            }
            else
            {
                IsExclusiveLocked = false;
                LockStatusText = "Paylaşımlı Mod (GCUBridge ile çakışabilir)";
            }
        }
        else
        {
            ConnectionStatusText = "Cihaz Bulunamadı";
            LockStatusText = status == LockStatus.RequiresElevation
                ? "Yönetici İzni Gerekli"
                : "Cihaz Açılamadı";
        }
    }

    public void ApplyEffect(IEffect effect)
    {
        _activeEffect = effect;

        // Only wake WASAPI audio capture & FFT when an audio-reactive effect is active
        _audioCapture.IsActiveConsumer = effect is AudioVisualizerEffect or AudioSplitEffect or MoodCreatureEffect;

        if (SupportsGenericSpeed(effect))
        {
            ApplyGenericSpeedToEffect(effect, _genericSpeedMultiplier);
        }

        RestoreEffectColorSettings(effect);
        _engine.SetEffect(effect);
        SaveCurrentState();
        ProfileOrEffectStateRestored?.Invoke();
    }

    private void RestoreEffectColorSettings(IEffect effect)
    {
        if (_perEffectColors.TryGetValue(effect.Id, out var saved))
        {
            _engine.ColorProcessor.Mode = (EffectColorMode)Math.Clamp(saved.ColorMode, 0, 4);
            _engine.ColorProcessor.PrimaryColor = ColorRgb.FromHex(saved.PrimaryColor);
            _engine.ColorProcessor.SecondaryColor = ColorRgb.FromHex(saved.SecondaryColor);
            _engine.ColorProcessor.DualBlendMode = (DualColorBlendMode)Math.Clamp(saved.DualBlendMode, 0, 3);
            _engine.ColorProcessor.RgbStyle = (RgbFlowStyle)Math.Clamp(saved.RgbStyle, 0, 2);
            _engine.ColorProcessor.StudioPaletteIndex = Math.Clamp(saved.StudioPaletteIndex, 0, GlobalColorProcessor.StudioPalettes.Length - 1);
            _engine.ColorProcessor.RgbSpeed = Math.Clamp(saved.RgbSpeed > 0 ? saved.RgbSpeed : 1.0, 0.2, 3.0);
        }
        else
        {
            GetDefaultColorsForEffect(effect, out var defPrimary, out var defSecondary);
            _engine.ColorProcessor.Mode = EffectColorMode.Default;
            _engine.ColorProcessor.PrimaryColor = defPrimary;
            _engine.ColorProcessor.SecondaryColor = defSecondary;
            _engine.ColorProcessor.DualBlendMode = DualColorBlendMode.SmartTone;
            _engine.ColorProcessor.RgbStyle = RgbFlowStyle.WaveDiagonal;
            _engine.ColorProcessor.StudioPaletteIndex = 0;
            _engine.ColorProcessor.RgbSpeed = 1.0;
        }

        SyncNativeEffectColors();
    }

    private void SaveActiveEffectColorSettings()
    {
        if (_activeEffect == null) return;
        _perEffectColors[_activeEffect.Id] = new EffectColorSettings
        {
            ColorMode = (int)_engine.ColorProcessor.Mode,
            PrimaryColor = _engine.ColorProcessor.PrimaryColor.ToHex(),
            SecondaryColor = _engine.ColorProcessor.SecondaryColor.ToHex(),
            DualBlendMode = (int)_engine.ColorProcessor.DualBlendMode,
            RgbStyle = (int)_engine.ColorProcessor.RgbStyle,
            StudioPaletteIndex = _engine.ColorProcessor.StudioPaletteIndex,
            RgbSpeed = _engine.ColorProcessor.RgbSpeed
        };
    }

    private void SyncNativeEffectColors()
    {
        if (_activeEffect == null) return;

        ColorRgb p = _engine.ColorProcessor.PrimaryColor;
        ColorRgb s = _engine.ColorProcessor.SecondaryColor;
        var mode = _engine.ColorProcessor.Mode;

        switch (_activeEffect)
        {
            case StaticEffect st:
                st.PrimaryColor = p;
                st.Zone2Color = s;
                break;
            case BreathingEffect br:
                br.PrimaryColor = p;
                if (mode is EffectColorMode.SingleColor or EffectColorMode.DualColor)
                    br.MultiColorMode = false;
                break;
            case ReactiveEffect rx:
                rx.SplashColor = p;
                if (mode is EffectColorMode.SingleColor or EffectColorMode.DualColor)
                    rx.RandomColorMode = false;
                break;
            case GamingModeEffect gm:
                gm.WasdColor = p;
                gm.ActionKeyColor = s;
                break;
            case ScanEffect sc:
                sc.BeamColor = p;
                break;
            case PulseEffect pl:
                pl.PrimaryColor = p;
                break;
            case StrobeEffect stb:
                stb.FlashColor = p;
                break;
        }
    }

    private void GetDefaultColorsForEffect(IEffect effect, out ColorRgb primary, out ColorRgb secondary)
    {
        switch (effect)
        {
            case StaticEffect st:
                primary = st.PrimaryColor;
                secondary = st.Zone2Color;
                break;
            case BreathingEffect br:
                primary = br.PrimaryColor;
                secondary = new ColorRgb(124, 58, 237);
                break;
            case ReactiveEffect rx:
                primary = rx.SplashColor;
                secondary = new ColorRgb(255, 0, 128);
                break;
            case GamingModeEffect gm:
                primary = gm.WasdColor;
                secondary = gm.ActionKeyColor;
                break;
            case ScanEffect sc:
                primary = sc.BeamColor;
                secondary = new ColorRgb(0, 240, 255);
                break;
            case PulseEffect pl:
                primary = pl.PrimaryColor;
                secondary = new ColorRgb(0, 240, 255);
                break;
            case StrobeEffect stb:
                primary = stb.FlashColor;
                secondary = new ColorRgb(124, 58, 237);
                break;
            case FlameEffect:
                primary = new ColorRgb(255, 90, 0);
                secondary = new ColorRgb(255, 210, 0);
                break;
            case MatrixEffect or DigitalRainEffect:
                primary = new ColorRgb(0, 255, 100);
                secondary = new ColorRgb(0, 240, 255);
                break;
            case LivingEyesEffect:
                primary = new ColorRgb(0, 240, 255);
                secondary = new ColorRgb(124, 58, 237);
                break;
            case CyberOwlEffect:
                primary = new ColorRgb(255, 210, 0);
                secondary = new ColorRgb(124, 58, 237);
                break;
            case DinoRunEffect or SnakeEffect:
                primary = new ColorRgb(34, 197, 94);
                secondary = new ColorRgb(255, 180, 0);
                break;
            default:
                primary = new ColorRgb(0, 240, 255);
                secondary = new ColorRgb(124, 58, 237);
                break;
        }
    }

    public void ApplyEffectById(string id)
    {
        var item = AllEffects.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            if (SelectedEffectItem == item)
            {
                ApplyEffect(item.Effect);
            }
            else
            {
                SelectedEffectItem = item;
            }
        }
    }

    public void ApplyProfile(string profileName)
    {
        var profile = _config.Profiles.Find(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                      ?? _config.Profiles.FirstOrDefault()
                      ?? new ProfileModel { Name = "Varsayılan", EffectId = "rainbow_wave" };

        _config.ActiveProfile = profile.Name;
        _brightness = profile.Brightness;
        _device.Brightness = _brightness;
        OnPropertyChanged(nameof(Brightness));

        // 1. Restore Static Effect
        Static.Mode = (StaticEffect.StaticLayoutMode)Math.Clamp(profile.StaticMode, 0, 2);
        Static.PrimaryColor = ColorRgb.FromHex(profile.StaticPrimaryColor);
        Static.Zone2Color = ColorRgb.FromHex(profile.StaticZone2Color);
        Static.Zone3Color = ColorRgb.FromHex(profile.StaticZone3Color);

        // 2. Restore Wave Effect
        Wave.Speed = Math.Clamp(profile.WaveSpeed, 0.2, 5.0);
        Wave.AngleDegrees = Math.Clamp(profile.WaveAngle, 0.0, 360.0);
        Wave.PaletteIndex = Math.Clamp(profile.WavePalette, 0, 3);

        // 3. Restore Breathing Effect
        Breathing.Speed = Math.Clamp(profile.BreathingSpeed, 0.2, 4.0);
        Breathing.MultiColorMode = profile.BreathingMultiColor;
        Breathing.PrimaryColor = ColorRgb.FromHex(profile.BreathingColor);

        // 4. Restore Reactive Effect
        Reactive.Mode = (ReactiveEffect.ReactiveMode)Math.Clamp(profile.ReactiveMode, 0, 2);
        Reactive.FadeDuration = Math.Clamp(profile.ReactiveDuration, 0.2, 3.0);
        Reactive.SplashColor = ColorRgb.FromHex(profile.ReactiveColor);
        Reactive.RandomColorMode = profile.ReactiveRandom;

        // 5. Restore Audio Visualizer Effect
        Visualizer.Mode = (AudioVisualizerEffect.VisualizerMode)Math.Clamp(profile.VisualizerMode, 0, 2);
        Visualizer.Sensitivity = Math.Clamp(profile.VisualizerSensitivity, 0.5, 4.0);
        Visualizer.PaletteIndex = Math.Clamp(profile.VisualizerPalette, 0, 2);

        // 6. Restore Gaming, Flame, Matrix, DigitalRain, Party
        GamingMode.Preset = (GamingModeEffect.GamePreset)Math.Clamp(profile.GamingPreset, 0, 2);

        Flame.Type = (FlameEffect.FlameType)Math.Clamp(profile.FlameType, 0, 2);
        Flame.Speed = Math.Clamp(profile.FlameSpeed, 0.5, 4.0);
        Flame.Turbulence = Math.Clamp(profile.FlameTurbulence, 0.2, 2.5);

        Matrix.Style = (MatrixEffect.MatrixStyle)Math.Clamp(profile.MatrixStyle, 0, 3);
        Matrix.ColorTheme = (MatrixEffect.MatrixColorTheme)Math.Clamp(profile.MatrixColorTheme, 0, 3);
        Matrix.Speed = Math.Clamp(profile.MatrixSpeed, 1.0, 10.0);
        Matrix.Density = Math.Clamp(profile.MatrixDensity, 0.1, 1.0);

        DigitalRain.Palette = (DigitalRainEffect.TorrentPalette)Math.Clamp(profile.DigitalRainPalette, 0, 2);
        DigitalRain.Speed = Math.Clamp(profile.DigitalRainSpeed, 0.5, 5.0);
        DigitalRain.GlitchFrequency = Math.Clamp(profile.DigitalRainGlitch, 0.1, 1.0);

        Party.Mode = Math.Clamp(profile.PartyMode, 0, 2);
        Party.Bpm = Math.Clamp(profile.PartyBpm, 60.0, 200.0);

        // 7. Restore Generic Speed Multiplier & Per-Key Painter Map
        _genericSpeedMultiplier = Math.Clamp(profile.GenericSpeedMultiplier > 0 ? profile.GenericSpeedMultiplier : 1.0, 0.2, 3.0);
        if (profile.PerKeyColors != null && profile.PerKeyColors.Count > 0)
        {
            StaticPainter.ImportHexMap(profile.PerKeyColors);
        }

        // 8. Restore Per-Effect Color Settings Map
        _perEffectColors.Clear();
        if (profile.PerEffectColors != null)
        {
            foreach (var kv in profile.PerEffectColors)
            {
                _perEffectColors[kv.Key] = kv.Value;
            }
        }

        // Ensure the profile's primary EffectId has its global color state synced
        if (!string.IsNullOrWhiteSpace(profile.EffectId) && !_perEffectColors.ContainsKey(profile.EffectId))
        {
            _perEffectColors[profile.EffectId] = new EffectColorSettings
            {
                ColorMode = profile.GlobalColorMode,
                PrimaryColor = profile.GlobalPrimaryColor,
                SecondaryColor = profile.GlobalSecondaryColor,
                DualBlendMode = profile.GlobalDualBlendMode,
                RgbStyle = profile.GlobalRgbStyle,
                StudioPaletteIndex = profile.GlobalPaletteIndex,
                RgbSpeed = profile.GlobalRgbSpeed > 0 ? profile.GlobalRgbSpeed : 1.0
            };
        }

        ApplyEffectById(profile.EffectId);
        ProfileOrEffectStateRestored?.Invoke();
    }

    public double GenericSpeedMultiplier
    {
        get => _genericSpeedMultiplier;
        set
        {
            double clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(_genericSpeedMultiplier - clamped) > 0.001)
            {
                _genericSpeedMultiplier = clamped;
                if (_activeEffect != null && SupportsGenericSpeed(_activeEffect))
                {
                    ApplyGenericSpeedToEffect(_activeEffect, _genericSpeedMultiplier);
                }
                SaveCurrentState();
                OnPropertyChanged();
            }
        }
    }

    public bool SupportsGenericSpeed(IEffect? effect)
    {
        return effect is RainbowWaveEffect or RippleEffect or MarqueeEffect or PinwheelEffect or ChevronEffect
            or AuroraEffect or RainEffect or LightningEffect or SparkleEffect or WheelEffect or FireworksEffect
            or ScanEffect or PulseEffect or StrobeEffect or JellybeanEffect or SpectrumCycleEffect or ColorCycleEffect
            or FluidEffect or PlasmaEffect or BlackHoleEffect or PerlinFogEffect or GlitchEffect or GameOfLifeEffect
            or DnaHelixEffect or AudioSplitEffect or TypingHeatmapEffect;
    }

    private static void ApplyGenericSpeedToEffect(IEffect effect, double mul)
    {
        switch (effect)
        {
            case RainbowWaveEffect rw: rw.Speed = 1.5 * mul; break;
            case RippleEffect rp: rp.Speed = 8.0 * mul; break;
            case MarqueeEffect mq: mq.Speed = 14.0 * mul; break;
            case PinwheelEffect pw: pw.Speed = 1.2 * mul; break;
            case ChevronEffect ch: ch.Speed = 4.0 * mul; break;
            case AuroraEffect au: au.Speed = 1.2 * mul; break;
            case RainEffect rn: rn.Speed = 6.0 * mul; break;
            case LightningEffect lt: lt.Frequency = 1.0 * mul; break;
            case SparkleEffect sp: sp.Speed = 1.2 * mul; break;
            case WheelEffect wh: wh.Speed = 1.0 * mul; break;
            case FireworksEffect fw: fw.Speed = 1.0 * mul; break;
            case ScanEffect sc: sc.Speed = 1.6 * mul; break;
            case PulseEffect pl: pl.Speed = 1.0 * mul; break;
            case StrobeEffect st: st.FrequencyHz = Math.Clamp(8.0 * mul, 0.5, 15.0); break;
            case JellybeanEffect jb: jb.Speed = 3.5 * mul; break;
            case SpectrumCycleEffect spc: spc.Speed = 0.5 * mul; break;
            case ColorCycleEffect cc: cc.Speed = 0.8 * mul; break;
            case FluidEffect fl: fl.Speed = 1.0 * mul; break;
            case PlasmaEffect pm: pm.Speed = 1.2 * mul; break;
            case BlackHoleEffect bh: bh.Speed = 1.0 * mul; break;
            case PerlinFogEffect pf: pf.Speed = 0.8 * mul; break;
            case GlitchEffect gl: gl.Speed = 1.4 * mul; break;
            case GameOfLifeEffect gol: gol.StepInterval = Math.Clamp(0.22 / Math.Max(0.25, mul), 0.06, 1.0); break;
            case DnaHelixEffect dna: dna.Speed = 1.3 * mul; break;
            case AudioSplitEffect aus: aus.Sensitivity = 2.0 * mul; break;
            case TypingHeatmapEffect th: th.CoolingRate = 0.35 * mul; break;
        }
    }

    public string GetEffectControlHint(IEffect? effect)
    {
        if (effect == null) return "Bu efekt otomatik akış modunda çalışır.";

        return effect.Id switch
        {
            "dino_run" => "🦖 Kontroller: [Space] veya [▲ Yukarı Ok] ile zıpla! Engellerden kaçarak rekor kır.",
            "snake" => "🐍 Kontroller: [W/A/S/D] veya [Ok Tuşları] ile yılanı yönlendir, altın elmaları topla!",
            "space_invaders" => "🚀 Kontroller: [◄/►] veya [A/D] ile gemiyi sür, [Space] ile ateş et, uzaylıları vur!",
            "breakout" => "🧱 Kontroller: [◄/►] veya [A/D] ile raketi kaydır, seken topla tüm neon tuğlaları kır!",
            "flappy_bird" => "🐤 Kontroller: [Space] veya [▲ Yukarı Ok] ile kanat çırp ve yaklaşan boruların arasından süzül!",
            "whack_a_mole" => "🔨 Kontroller: Harflerde aniden parlayan köstebeklere sönmeden önce hızla bas!",
            "pong" => "🏓 Kontroller: [▲/▼] veya [W/S] ile sağ raketi yönet, yapay zeka rakibe karşı 5 puana ulaş!",
            "reaction_test" => "🎯 Kontroller: Mor bekleme geriliminden sonra KIRMIZI parlayan tuşa en kısa sürede bas!",
            "typing_rain" => "🌧️ İnteraktif: Bastığın her tuştan yağmur damlaları süzülür ve alt satırda su birikintisi oluşturur.",
            "cursor_ghost" => "👻 İnteraktif: Fare imlecini ekranda gezdirdikçe klavye üzerinde neon kuyruklu yıldız seni takip eder.",
            "living_eyes" => "👁️ Canlı Varlık: Gözler fare imlecini gerçek zamanlı izler, kırpar, yazarken odaklanır ve boşta uyur.",
            "virtual_pet" => "🐉 Canlı Varlık: Yazı yazdıkça beslenir ve mutlu olur! [Space] tuşuna basarak zıplatabilirsin.",
            "keyboard_buddy" => "🤖 Canlı Varlık: Bastığın tuşların yanına merakla koşar ve etrafına neşeli siber kıvılcımlar saçar.",
            "mood_creature" => "🦎 Canlı Varlık: Sistem CPU yüküne ve çalan müziğin bas ritmine göre ruh hali ve rengi evrilir.",
            "cyber_owl" => "🦉 Canlı Varlık: Üst F-tuşları dalında tüneyen bilge siber baykuş fareyi izler, yazarken kanat çırpar.",
            "ant_colony" => "🐜 Canlı Varlık: Bastığın tuşlarda yem kırıntıları oluşur; karıncalar yemleri koloni yuvasına taşır.",
            "system_pulse" => "💓 Donanım Telemetrisi: Gerçek zamanlı CPU yüküne göre kalp atış hızı ve ısı rengi otomatik değişir.",
            _ => "Aşağıdaki kontroller ile efektin akış hızını ayarlayabilir veya animasyonu sıfırlayabilirsiniz."
        };
    }

    public void ResetActiveEffect()
    {
        _engine.ResetActiveEffect();
    }

    public bool CreateProfile(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string trimmed = name.Trim();

        var existing = _config.Profiles.FirstOrDefault(p => p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SnapshotStateToProfile(existing);
            if (!string.IsNullOrWhiteSpace(description))
            {
                existing.CustomDescription = description;
            }
            _config.ActiveProfile = trimmed;
            DoSaveConfig();
            LoadProfiles();
            return true;
        }

        var newProfile = new ProfileModel
        {
            Name = trimmed,
            CustomDescription = description
        };
        SnapshotStateToProfile(newProfile);

        _config.Profiles.Add(newProfile);
        _config.ActiveProfile = trimmed;
        DoSaveConfig();
        LoadProfiles();
        return true;
    }

    public bool DeleteProfile(ProfileModel profile)
    {
        if (_config.Profiles.Count <= 1) return false;
        _config.Profiles.Remove(profile);
        Profiles.Remove(profile);

        if (_config.ActiveProfile.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = _config.Profiles.FirstOrDefault();
            if (fallback != null)
            {
                ApplyProfile(fallback.Name);
            }
        }
        SaveCurrentState();
        return true;
    }

    public bool ExportProfile(ProfileModel profile, string filePath)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(profile, options);
            System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            return true;
        }
        catch { return false; }
    }

    public bool ImportProfile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return false;
            string json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var imported = System.Text.Json.JsonSerializer.Deserialize<ProfileModel>(json);
            if (imported == null || string.IsNullOrWhiteSpace(imported.Name)) return false;

            string baseName = imported.Name;
            int counter = 1;
            while (_config.Profiles.Any(p => p.Name.Equals(imported.Name, StringComparison.OrdinalIgnoreCase)))
            {
                imported.Name = $"{baseName} ({counter++})";
            }

            _config.Profiles.Add(imported);
            LoadProfiles();
            ApplyProfile(imported.Name);
            SaveCurrentState();
            return true;
        }
        catch { return false; }
    }

    public void SaveCurrentState()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SnapshotStateToProfile(ProfileModel profile)
    {
        if (_activeEffect != null)
        {
            profile.EffectId = _activeEffect.Id;
        }
        profile.Brightness = _brightness;

        SaveActiveEffectColorSettings();
        profile.GlobalColorMode = (int)_engine.ColorProcessor.Mode;
        profile.GlobalPrimaryColor = _engine.ColorProcessor.PrimaryColor.ToHex();
        profile.GlobalSecondaryColor = _engine.ColorProcessor.SecondaryColor.ToHex();
        profile.GlobalDualBlendMode = (int)_engine.ColorProcessor.DualBlendMode;
        profile.GlobalRgbStyle = (int)_engine.ColorProcessor.RgbStyle;
        profile.GlobalPaletteIndex = _engine.ColorProcessor.StudioPaletteIndex;
        profile.GlobalRgbSpeed = _engine.ColorProcessor.RgbSpeed;

        profile.PerEffectColors = new Dictionary<string, EffectColorSettings>(_perEffectColors, StringComparer.OrdinalIgnoreCase);

        profile.StaticMode = (int)Static.Mode;
        profile.StaticPrimaryColor = Static.PrimaryColor.ToHex();
        profile.StaticZone2Color = Static.Zone2Color.ToHex();
        profile.StaticZone3Color = Static.Zone3Color.ToHex();

        profile.WaveSpeed = Wave.Speed;
        profile.WaveAngle = Wave.AngleDegrees;
        profile.WavePalette = Wave.PaletteIndex;

        profile.BreathingSpeed = Breathing.Speed;
        profile.BreathingMultiColor = Breathing.MultiColorMode;
        profile.BreathingColor = Breathing.PrimaryColor.ToHex();

        profile.ReactiveMode = (int)Reactive.Mode;
        profile.ReactiveDuration = Reactive.FadeDuration;
        profile.ReactiveColor = Reactive.SplashColor.ToHex();
        profile.ReactiveRandom = Reactive.RandomColorMode;

        profile.VisualizerMode = (int)Visualizer.Mode;
        profile.VisualizerSensitivity = Visualizer.Sensitivity;
        profile.VisualizerPalette = Visualizer.PaletteIndex;

        profile.GamingPreset = (int)GamingMode.Preset;

        profile.FlameType = (int)Flame.Type;
        profile.FlameSpeed = Flame.Speed;
        profile.FlameTurbulence = Flame.Turbulence;

        profile.MatrixStyle = (int)Matrix.Style;
        profile.MatrixColorTheme = (int)Matrix.ColorTheme;
        profile.MatrixSpeed = Matrix.Speed;
        profile.MatrixDensity = Matrix.Density;

        profile.DigitalRainPalette = (int)DigitalRain.Palette;
        profile.DigitalRainSpeed = DigitalRain.Speed;
        profile.DigitalRainGlitch = DigitalRain.GlitchFrequency;

        profile.PartyMode = Party.Mode;
        profile.PartyBpm = Party.Bpm;

        profile.GenericSpeedMultiplier = _genericSpeedMultiplier;
        profile.PerKeyColors = StaticPainter.ExportHexMap();
    }

    private void DoSaveConfig()
    {
        var profile = _config.Profiles.Find(p => p.Name.Equals(_config.ActiveProfile, StringComparison.OrdinalIgnoreCase));
        if (profile != null && _activeEffect != null)
        {
            SnapshotStateToProfile(profile);
            System.Threading.Tasks.Task.Run(() => ConfigManager.SaveConfig(_config));
        }
    }

    public void UpdateWpm()
    {
        TypingHeatmap.RefreshWpm();
        int wpm = (int)Math.Round(TypingHeatmap.CurrentWpm);
        int strokes = (int)TypingHeatmap.TotalKeystrokes;
        bool isHeatmap = _activeEffect is TypingHeatmapEffect;

        if (!isHeatmap && wpm == _lastWpmValue && _lastTotalKeystrokes == -1)
            return;
        if (isHeatmap && wpm == _lastWpmValue && strokes == _lastTotalKeystrokes)
            return;

        _lastWpmValue = wpm;
        _lastTotalKeystrokes = isHeatmap ? strokes : -1;

        WpmText = isHeatmap
            ? $"{wpm} WPM ({strokes} tuş)"
            : $"{wpm} WPM";
    }

    public void Dispose()
    {
        _saveDebounceTimer.Stop();
        DoSaveConfig();
        _engine.Dispose();
        _audioCapture.Dispose();
        _device.Dispose();
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    #endregion
}
