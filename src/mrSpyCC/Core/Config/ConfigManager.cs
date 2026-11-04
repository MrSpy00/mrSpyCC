using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace mrSpyCC.Core.Config;

public static class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string ConfigFilePath
    {
        get
        {
            string? exe = Environment.ProcessPath;
            string dir = string.IsNullOrEmpty(exe) ? AppContext.BaseDirectory : (Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory);
            return Path.Combine(dir, "mrspycc_config.json");
        }
    }

    public static AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg != null && cfg.Profiles.Count > 0)
                {
                    cfg.FavoriteEffectIds ??= ["wave", "reactive", "fluid", "black_hole"];
                    return cfg;
                }
            }
        }
        catch
        {
            // Fall back to default
        }

        return CreateDefaultConfig();
    }

    public static void SaveConfig(AppConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // Silently ignore or log
        }
    }

    public static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            ActiveProfile = "Oyun Modu",
            MinimizeToTray = true,
            AutoStartWithWindows = false,
            FavoriteEffectIds = ["wave", "reactive", "fluid", "black_hole"],
            Profiles =
            [
                new ProfileModel
                {
                    Name = "Oyun Modu",
                    EffectId = "wave",
                    Brightness = 100,
                    WaveSpeed = 1.6,
                    WaveAngle = 45.0,
                    WavePalette = 1 // Cyberpunk
                },
                new ProfileModel
                {
                    Name = "Reaktif Su Dalgası",
                    EffectId = "reactive",
                    Brightness = 100,
                    ReactiveMode = 1, // Ripple
                    ReactiveColor = "#00F0FF",
                    ReactiveDuration = 1.0
                },
                new ProfileModel
                {
                    Name = "Müzik / Visualizer",
                    EffectId = "audio_visualizer",
                    Brightness = 100,
                    VisualizerMode = 0,
                    VisualizerSensitivity = 1.8,
                    VisualizerPalette = 1
                },
                new ProfileModel
                {
                    Name = "Gece / Gizlilik",
                    EffectId = "breathing",
                    Brightness = 35,
                    BreathingSpeed = 0.6,
                    BreathingMultiColor = false,
                    BreathingColor = "#7C3AED"
                },
                new ProfileModel
                {
                    Name = "Siber Karadelik",
                    EffectId = "black_hole",
                    Brightness = 95,
                    CustomDescription = "Merkez tekillik kütleçekim çekimi ve süpernova patlama döngüsü."
                },
                new ProfileModel
                {
                    Name = "Sıvı & Plazma Deneyi",
                    EffectId = "fluid",
                    Brightness = 90,
                    CustomDescription = "Akışkanlar mekaniği, viskozite ve dalga simülasyonu."
                }
            ]
        };
    }
}
