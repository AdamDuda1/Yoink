using System;
using System.IO;

namespace Yoink_Downloader.Services
{
    /// <summary>
    /// Works out which copy of each external tool to run, in this order:
    ///
    ///   1. an explicit path the user set on the Settings page
    ///   2. a copy bundled with the app, in a Tools folder next to the exe
    ///   3. the bare exe name, which lets Windows search PATH
    ///
    /// </summary>
    public static class ToolPaths
    {
        public static string? YtDlpOverride { get; set; }
        public static string? FfmpegOverride { get; set; }
        public static string? Aria2Override { get; set; }

        public static string YtDlp => Resolve("yt-dlp.exe", YtDlpOverride);

        public static string Ffmpeg => Resolve("ffmpeg.exe", FfmpegOverride);

        public static string Aria2 => Resolve("aria2c.exe", Aria2Override);

        private static string BundledFolder => Path.Combine(AppContext.BaseDirectory, "Tools");

        public static bool IsResolved(string toolPath) => Path.IsPathRooted(toolPath);

        private static string Resolve(string exeName, string? overridePath)
        {
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            var bundled = Path.Combine(BundledFolder, exeName);
            if (File.Exists(bundled))
            {
                return bundled;
            }

            return exeName; // 1-3 failed, using path. if not in path, throws Win32Exception
        }
    }
}
