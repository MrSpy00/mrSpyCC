using System.Collections.Generic;
using mrSpyCC.Core.Engine;

namespace mrSpyCC.Core.Config;

public sealed class EffectColorSettings
{
    public int ColorMode { get; set; } = (int)EffectColorMode.Default;
    public string PrimaryColor { get; set; } = "#00F0FF";
    public string SecondaryColor { get; set; } = "#7C3AED";
    public int DualBlendMode { get; set; } = (int)DualColorBlendMode.SmartTone;
    public int RgbStyle { get; set; } = (int)RgbFlowStyle.WaveDiagonal;
    public int StudioPaletteIndex { get; set; } = 0;
    public double RgbSpeed { get; set; } = 1.0;
}

public sealed class ProfileModel
{
    public string Name { get; set; } = "Varsayılan";
    public string EffectId { get; set; } = "wave";
    public int Brightness { get; set; } = 100;
    public string? CustomDescription { get; set; }

    // Global Color & RGB Engine settings for the active effect in this profile
    public int GlobalColorMode { get; set; } = (int)EffectColorMode.Default;
    public string GlobalPrimaryColor { get; set; } = "#00F0FF";
    public string GlobalSecondaryColor { get; set; } = "#7C3AED";
    public int GlobalDualBlendMode { get; set; } = (int)DualColorBlendMode.SmartTone;
    public int GlobalRgbStyle { get; set; } = (int)RgbFlowStyle.WaveDiagonal;
    public int GlobalPaletteIndex { get; set; } = 0;
    public double GlobalRgbSpeed { get; set; } = 1.0;

    // Per-Effect saved color/RGB overrides (keyed by Effect.Id)
    public Dictionary<string, EffectColorSettings> PerEffectColors { get; set; } = new();

    // Effect settings
    public int StaticMode { get; set; } = 0; // 0=Uniform, 1=ThreeZone, 2=Wasd
    public string StaticPrimaryColor { get; set; } = "#7C3AED";
    public string StaticZone2Color { get; set; } = "#00F0FF";
    public string StaticZone3Color { get; set; } = "#FF0080";

    public double WaveSpeed { get; set; } = 1.5;
    public double WaveAngle { get; set; } = 90.0;
    public int WavePalette { get; set; } = 0;

    public double BreathingSpeed { get; set; } = 1.0;
    public bool BreathingMultiColor { get; set; } = true;
    public string BreathingColor { get; set; } = "#00F0FF";

    public int ReactiveMode { get; set; } = 1; // 0=Single, 1=Ripple, 2=Laser
    public double ReactiveDuration { get; set; } = 1.0;
    public string ReactiveColor { get; set; } = "#00F0FF";
    public bool ReactiveRandom { get; set; } = false;

    public int VisualizerMode { get; set; } = 0;
    public double VisualizerSensitivity { get; set; } = 1.8;
    public int VisualizerPalette { get; set; } = 1;

    public int GamingPreset { get; set; } = 0;

    public int FlameType { get; set; } = 0;
    public double FlameSpeed { get; set; } = 1.6;
    public double FlameTurbulence { get; set; } = 1.0;

    public int MatrixStyle { get; set; } = 0;
    public int MatrixColorTheme { get; set; } = 0;
    public double MatrixSpeed { get; set; } = 4.0;
    public double MatrixDensity { get; set; } = 0.45;

    public int DigitalRainPalette { get; set; } = 0;
    public double DigitalRainSpeed { get; set; } = 2.4;
    public double DigitalRainGlitch { get; set; } = 0.85;

    public int PartyMode { get; set; } = 0;
    public double PartyBpm { get; set; } = 128.0;

    public double GenericSpeedMultiplier { get; set; } = 1.0;

    public Dictionary<int, string> PerKeyColors { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public string Description => !string.IsNullOrEmpty(CustomDescription) ? CustomDescription : Name switch
    {
        "Oyun Modu" => "WASD ve ok tuşları ön planda; akıcı siber renk dalgası ve yüksek tempolu aydınlatma.",
        "Reaktif Su Dalgası" => "Her tuş vuruşunda merkezden dışa yayılan hipnotik camgöbeği su halkaları.",
        "Müzik / Visualizer" => "Windows sistem sesine ve bas frekanslarına senkronize 31-bant spektrum ekolayzer.",
        "Gece / Gizlilik" => "Gözü yormayan loş derin mor nefes solunumu ve odaklanma aydınlatması.",
        "Sıvı & Plazma Deneyi" => "Akışkanlar mekaniği ve organik lav lambası alan simülasyonu.",
        "Siber Karadelik" => "Merkez tekillik kütleçekim çekimi ve süpernova patlama döngüsü.",
        _ => $"Özelleştirilmiş aydınlatma profili ({EffectId} modu, %{Brightness} parlaklık)."
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public string[] PaletteColors
    {
        get
        {
            var mode = (EffectColorMode)GlobalColorMode;
            if (mode == EffectColorMode.SingleColor)
            {
                return [GlobalPrimaryColor, GlobalPrimaryColor, GlobalPrimaryColor, "#1E293B"];
            }
            if (mode == EffectColorMode.DualColor)
            {
                return [GlobalPrimaryColor, GlobalSecondaryColor, GlobalPrimaryColor, GlobalSecondaryColor];
            }
            if (mode == EffectColorMode.RainbowRgb)
            {
                return ["#FF0055", "#00F0FF", "#22C55E", "#FACC15"];
            }
            if (mode == EffectColorMode.StudioPalette)
            {
                var p = GlobalColorProcessor.GetPalette(GlobalPaletteIndex);
                return [p.Colors[0].ToHex(), p.Colors[1].ToHex(), p.Colors[2].ToHex(), p.Colors[3].ToHex()];
            }

            return Name switch
            {
                "Oyun Modu" => ["#7C3AED", "#00F0FF", "#EC4899", "#3B82F6"],
                "Reaktif Su Dalgası" => ["#00F0FF", "#38BDF8", "#0284C7", "#E0F2FE"],
                "Müzik / Visualizer" => ["#10B981", "#F59E0B", "#EF4444", "#8B5CF6"],
                "Gece / Gizlilik" => ["#7C3AED", "#4C1D95", "#312E81", "#1E1B4B"],
                _ => EffectId switch
                {
                    "black_hole" => ["#8B5CF6", "#F59E0B", "#EF4444", "#FFFFFF"],
                    "fluid" => ["#00F0FF", "#0284C7", "#38BDF8", "#E0F2FE"],
                    "plasma" => ["#EC4899", "#8B5CF6", "#F59E0B", "#EF4444"],
                    "perlin_fog" => ["#C084FC", "#38BDF8", "#818CF8", "#F8FAFC"],
                    "glitch" => ["#00F0FF", "#EF4444", "#FACC15", "#8B5CF6"],
                    "game_of_life" => ["#00F0FF", "#22C55E", "#F59E0B", "#10B981"],
                    "dna_helix" => ["#00F0FF", "#EC4899", "#F59E0B", "#10B981"],
                    "system_pulse" => ["#00F0FF", "#8B5CF6", "#F59E0B", "#EF4444"],
                    "audio_split" => ["#EC4899", "#00F0FF", "#FACC15", "#FFFFFF"],
                    "flame" => ["#FF3200", "#FF7800", "#FFC800", "#FFFFFF"],
                    "matrix" => ["#22C55E", "#15803D", "#4ADE80", "#FFFFFF"],
                    "living_eyes" => ["#00F0FF", "#7C3AED", "#FFFFFF", "#1E1B4B"],
                    "dino_run" => ["#22C55E", "#EF4444", "#F59E0B", "#38BDF8"],
                    "snake" => ["#22C55E", "#F59E0B", "#EF4444", "#00F0FF"],
                    "virtual_pet" => ["#00F0FF", "#22C55E", "#EC4899", "#F59E0B"],
                    "space_invaders" => ["#00F0FF", "#EC4899", "#22C55E", "#FFFFFF"],
                    "pong" => ["#00F0FF", "#EC4899", "#FFFFFF", "#1E1B4B"],
                    "reaction_test" => ["#EF4444", "#22C55E", "#F59E0B", "#8B5CF6"],
                    "keyboard_buddy" => ["#00F0FF", "#F59E0B", "#8B5CF6", "#FFFFFF"],
                    "mood_creature" => ["#00F0FF", "#22C55E", "#EF4444", "#EC4899"],
                    "typing_rain" => ["#00F0FF", "#38BDF8", "#0284C7", "#E0F2FE"],
                    "cursor_ghost" => ["#8B5CF6", "#00F0FF", "#EC4899", "#FFFFFF"],
                    "breakout" => ["#FF283C", "#00E5FF", "#00FF8C", "#FFFFFF"],
                    "flappy_bird" => ["#FFD700", "#00DC3C", "#38BDF8", "#FF7800"],
                    "whack_a_mole" => ["#EC4899", "#00FF78", "#F59E0B", "#8B5CF6"],
                    "cyber_owl" => ["#FFD700", "#7C3AED", "#FF8C00", "#1E1B4B"],
                    "ant_colony" => ["#00F0FF", "#FFD700", "#10B981", "#3B82F6"],
                    _ => ["#7C3AED", "#A855F7", "#38BDF8", "#22C55E"]
                }
            };
        }
    }
}

public sealed class AppConfig
{
    public string ActiveProfile { get; set; } = "Varsayılan";
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoStartWithWindows { get; set; } = false;
    public List<string> FavoriteEffectIds { get; set; } = ["wave", "living_eyes", "dino_run", "virtual_pet", "snake", "fluid", "black_hole"];
    public List<ProfileModel> Profiles { get; set; } = new();
}
