using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yoink_Downloader.Services
{
    public class YtDlpService
    {
        /// <summary>Same format string tr() used - merged mp4/m4a, falling back to whatever is best.</summary>
        public const string DefaultFormat = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best";

        public const string DefaultTemplate = "%(title)s.%(ext)s";

        /// <summary>
        /// Set this when ffmpeg is not on PATH (yours lives in Downloads). Left null, we
        /// simply do not pass --ffmpeg-location and yt-dlp looks for it itself.
        /// </summary>
        public static string? FfmpegPath { get; set; }

        // yt-dlp prints progress as "[download]  42.3% of 12.34MiB at ..."
        private static readonly Regex ProgressPattern =
            new(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

        /// <summary>
        /// Runs "yt-dlp -J" and returns the video's metadata without downloading anything.
        /// </summary>
        public static async Task<VideoInfo> FetchInfoAsync(string url)
        {
            using var process = new Process();
            var startInfo = process.StartInfo;

            startInfo.FileName = "yt-dlp.exe";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.ArgumentList.Add("-J");
            startInfo.ArgumentList.Add("--no-playlist");
            startInfo.ArgumentList.Add(url);

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var json = await stdoutTask;
            var error = await stderrTask;

            if (process.ExitCode != 0 || json.Length == 0)
            {
                throw new InvalidOperationException(
                    error.Length > 0 ? error.Trim() : "yt-dlp could not read that link.");
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return new VideoInfo(
                Title: GetString(root, "title") ?? "(untitled)",
                Uploader: GetString(root, "uploader") ?? GetString(root, "channel") ?? "",
                Duration: root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                    ? TimeSpan.FromSeconds(d.GetDouble())
                    : TimeSpan.Zero,
                ThumbnailUrl: PickThumbnail(root));

            static string? GetString(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            static string? PickThumbnail(JsonElement root)
            {
                if (root.TryGetProperty("thumbnails", out var thumbnails) &&
                    thumbnails.ValueKind == JsonValueKind.Array)
                {
                    string? best = null;
                    foreach (var entry in thumbnails.EnumerateArray())
                    {
                        var url = GetString(entry, "url");
                        if (url is not null &&
                            (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
                             url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                             url.Contains(".png", StringComparison.OrdinalIgnoreCase)))
                        {
                            best = url;
                        }
                    }

                    if (best is not null)
                    {
                        return best;
                    }
                }

                return GetString(root, "thumbnail");
            }
        }

        /// <summary>
        /// Runs yt-dlp and returns its exit code (0 means success).
        /// Reports percentage through <paramref name="progress"/> and every output line
        /// through <paramref name="log"/>.
        /// </summary>
        public static async Task<int> DownloadAsync(
            string url,
            string outputFolder,
            string? fileNameTemplate = null,
            IProgress<double>? progress = null,
            IProgress<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("No URL given.", nameof(url));
            }

            Directory.CreateDirectory(outputFolder);

            using var process = new Process();
            var startInfo = process.StartInfo;

            startInfo.FileName = "yt-dlp.exe";
            // Running *in* the target folder means the -o template stays a bare file name,
            // which sidesteps any quoting trouble with paths that contain spaces.
            startInfo.WorkingDirectory = outputFolder;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(DefaultFormat);

            if (!string.IsNullOrWhiteSpace(FfmpegPath))
            {
                startInfo.ArgumentList.Add("--ffmpeg-location");
                startInfo.ArgumentList.Add(FfmpegPath);
            }

            // Without this yt-dlp redraws one progress line using \r, which never arrives
            // as a completed line and so never reaches OutputDataReceived.
            startInfo.ArgumentList.Add("--newline");

            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(fileNameTemplate)
                ? DefaultTemplate
                : fileNameTemplate);

            startInfo.ArgumentList.Add(url);

            // Both of these fire on a threadpool thread, never the UI thread. Reporting
            // through IProgress is what gets the values safely back to the UI - see the
            // comment where the Progress<T> objects are created in DownloadPage.
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                log?.Report(e.Data);

                var match = ProgressPattern.Match(e.Data);
                if (match.Success &&
                    // InvariantCulture matters: yt-dlp always prints "42.3", but on a
                    // Polish system the default parse expects "42,3" and would fail.
                    double.TryParse(match.Groups[1].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var percent))
                {
                    progress?.Report(percent);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    log?.Report(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // The async wait keeps the UI responsive - tr()'s WaitForExit() would have
            // frozen the whole window until the download finished.
            await process.WaitForExitAsync();

            // Returns immediately (the process is already gone) but flushes the last of
            // the redirected output, so no final lines get lost.
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}
