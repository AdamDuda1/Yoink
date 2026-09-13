using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Yoink_Downloader.Services
{
    public class Settings
    {
        /// <summary>Matches Microsoft.UI.Xaml.ElementTheme's names: Default/Light/Dark.</summary>
        public string Theme { get; set; } = "Default";
        public string PaneDisplayMode { get; set; } = "Left";
        public string? YtDlpPath { get; set; }
        public string? FfmpegPath { get; set; }
        public string? Aria2Path { get; set; }
        public VideoDownloadOptions VideoDownloadOptions { get; set; } = new();
    }

    public class VideoDownloadOptions
    {
        public bool EmbedThumbnail { get; set; } = false;
        public bool EmbedSubtitles { get; set; } = false;
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
                    Debug.WriteLine("Settings loaded.");
                }
                else
                {
                    Current = new Settings();
                    Debug.WriteLine("Settings object created (couldn't find file).");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading settings from file: {ex.Message}");
                Current = new Settings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataFolderPath);
                Debug.WriteLine($"Folder exists: {Directory.Exists(DataFolderPath)}");
                var json = JsonSerializer.Serialize(Current, _jsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                Debug.WriteLine($"Writing settings to: {SettingsFilePath}");
                Debug.WriteLine("Settings should be saved now.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while writing settings to file: {ex.Message}");
            }
        }
    }
}