using System;
using System.IO;
using System.Text.Json;

namespace Yoink_Downloader.Services
{
    public class Settings
    {
        public string Theme { get; set; } = "System"; // System/Dark/Light TODO check if api class members match
        public VideoDownloadOptions VideoDownloadOptions { get; set; } = new();
    }

    public class VideoDownloadOptions
    {
        public bool EmbedChapters { get; set; } = false;
    }

    public class SettingsService
    {
        private static readonly string DataFolderPath = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YoinkDownloader");
        private static readonly string SettingsFilePath = Path.Combine(DataFolderPath, "settings.json");

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true // TODO ?
        };

        public Settings Current { get; private set; } = new();

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                else
                {
                    Current = new Settings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while loading settings from file: {ex.Message}");
                Current = new Settings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataFolderPath);
                var json = JsonSerializer.Serialize(Current, _jsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while writing settings to file: {ex.Message}");
            }
        }
    }
}