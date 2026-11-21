using System;
using System.IO;
using System.Text.Json;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.Services
{
    public class SettingsManager
    {
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;
        private AppSettings? _cachedSettings;

        public SettingsManager(string rootDirectory)
        {
            _settingsPath = Path.Combine(rootDirectory, "Build", "dspc-settings.json");
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public AppSettings Load()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                    // Older files only have the preset index ("rumble"); make the percent explicit.
                    _cachedSettings.NormalizeRumble();
                    // 0.1.x files: frame rate 0..6 -> 30 / 60 / 120, and their "quality (4K)" choice -> standard.
                    if (_cachedSettings.FpsVersion < 3)
                    {
                        var old = _cachedSettings.Fps;
                        _cachedSettings.Fps = old == 0 ? 0 : old == 6 ? 2 : 1;
                        _cachedSettings.Lighting = 0;
                        _cachedSettings.FpsVersion = 3;
                    }
                    return _cachedSettings;
                }
                catch
                {
                    _cachedSettings = new AppSettings();
                    return _cachedSettings;
                }
            }

            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }

        public void Save(AppSettings settings)
        {
            _cachedSettings = settings;
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }

        public string GetSettingsPath() => _settingsPath;
    }
}