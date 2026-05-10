using System;
using System.IO;
using System.Text.Json;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// data/config.json の読み書きを行う。
/// </summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigDir => Path.Combine(AppContext.BaseDirectory, "data");
    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var def = CreateDefault();
                Save(def);
                return def;
            }
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, _options);
            return cfg ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            TrackedProcesses = new()
            {
                new TrackedProcess { ProcessName = "Unity", DisplayName = "Unity", Enabled = true },
                new TrackedProcess { ProcessName = "blender", DisplayName = "Blender", Enabled = true },
                new TrackedProcess { ProcessName = "TouchDesigner", DisplayName = "TouchDesigner", Enabled = true },
                new TrackedProcess { ProcessName = "AfterFX", DisplayName = "After Effects", Enabled = true },
            }
        };
    }
}
