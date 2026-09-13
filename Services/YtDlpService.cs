using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yoink_Downloader.Services
{
    /// <summary>What we pull out of yt-dlp's JSON dump. Add fields as you need them.</summary>
    public record VideoInfo(
        string Id,
        string Title,
        string Uploader,
        string UploaderLink,
        TimeSpan Duration,
        string? ThumbnailUrl,
        string PayloadSize,
        string VideoLink,
        IReadOnlyList<VideoFormat> Formats,
        IReadOnlyList<VideoThumbnail> Thumbnails)
    {
        /// <summary>Distinct heights, biggest first - ready for a quality dropdown.</summary>
        public IReadOnlyList<int> AvailableHeights => MediaFormats.AvailableHeights(Formats);
    }

    public class YtDlpService
    {
        public const string DefaultFormat = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best";

        public const string DefaultTemplate = "%(title)s.%(ext)s";

        /// <summary>Cached info is only reused for this long - YouTube's format URLs are signed and expire.</summary>
        private static readonly TimeSpan InfoCacheLifetime = TimeSpan.FromMinutes(30);

        // yt-dlp prints progress as "[download]  42.3% of 12.34MiB at ..."
        private static readonly Regex ProgressPattern =
            new(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

        public static DateTime LastInfoFetchTime;
        public static string? LastVideoInfoJson;
        private static VideoInfo? LastVideoInfo;

        /// <summary>The URL that was actually typed, which may differ from webpage_url.</summary>
        private static string? LastRequestedUrl;

        /// <summary>
        /// Runs "yt-dlp -J" and returns the video's metadata without downloading anything.
        /// </summary>
        public static async Task<VideoInfo> FetchInfoAsync(string url)
        {
            using var process = new Process();
            var startInfo = process.StartInfo;

            startInfo.FileName = ToolPaths.YtDlp;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.ArgumentList.Add("-J");
            startInfo.ArgumentList.Add("--no-playlist");
            startInfo.ArgumentList.Add(url);

            process.Start();

            // Start both reads before waiting. If you wait first, a full pipe buffer can
            // block yt-dlp forever and neither side ever moves.
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

            LastInfoFetchTime = DateTime.UtcNow;
            LastRequestedUrl = url;
            LastVideoInfoJson = json;

            return LastVideoInfo = new VideoInfo(
                Id: GetString(root, "id") ?? "video with no id??? wtf? error????",
                Title: GetString(root, "title") ?? "(untitled)",
                Uploader: GetString(root, "uploader") ?? GetString(root, "channel") ?? "",
                UploaderLink: GetString(root, "channel_url") ?? "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Duration: root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                    ? TimeSpan.FromSeconds(d.GetDouble())
                    : TimeSpan.Zero,
                ThumbnailUrl: PickThumbnail(root),
                PayloadSize: DescribeSize(System.Text.Encoding.UTF8.GetByteCount(json)),
                VideoLink: GetString(root, "webpage_url") ?? "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Formats: MediaFormats.ParseFormats(root),
                Thumbnails: MediaFormats.ParseThumbnails(root));

            static string? GetString(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            static string DescribeSize(double bytes)
            {
                if (bytes < 1024)
                {
                    return bytes.ToString("F2", CultureInfo.InvariantCulture) + "B";
                }

                var kilobytes = bytes / 1024.0;
                if (kilobytes < 1024)
                {
                    return kilobytes.ToString("F2", CultureInfo.InvariantCulture) + "KB";
                }

                return (kilobytes / 1024.0).ToString("F2", CultureInfo.InvariantCulture) + "MB";
            }

            // The top-level "thumbnail" is often a .webp, which WIC cannot always decode -
            // and a BitmapImage that fails to load just silently stays blank. The
            // thumbnails array is ordered smallest to largest, so take the last jpg/png.
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

        /// <summary>Runs yt-dlp and returns its exit code (0 means success).</summary>
        public static async Task<int> DownloadAsync(
            DownloadRequest request,
            IProgress<double>? progress = null,
            IProgress<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                throw new ArgumentException("No URL given.", nameof(request));
            }

            Directory.CreateDirectory(request.OutputFolder);

            // If we already fetched this exact link recently, hand yt-dlp the JSON we
            // still have instead of making it extract everything a second time. When the
            // info file is used, the URL is NOT passed - yt-dlp takes it from the file.
            string? infoJsonPath = null;
            if (LastVideoInfoJson is not null &&
                LastRequestedUrl is not null &&
                string.Equals(LastRequestedUrl, request.Url, StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - LastInfoFetchTime < InfoCacheLifetime)
            {
                infoJsonPath = Path.Combine(Path.GetTempPath(), $"yoink-{Guid.NewGuid():N}.info.json");
                File.WriteAllText(infoJsonPath, LastVideoInfoJson);
            }

            try
            {
                using var process = new Process();
                var startInfo = process.StartInfo;

                startInfo.FileName = ToolPaths.YtDlp;
                // Running *in* the target folder means the -o template stays a bare file
                // name, sidestepping quoting trouble with paths that contain spaces.
                startInfo.WorkingDirectory = request.OutputFolder;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add(DefaultFormat);

                // Only worth passing when we know a real path. If it resolved to a bare
                // name, yt-dlp searches PATH itself and does a better job of it.
                if (ToolPaths.IsResolved(ToolPaths.Ffmpeg))
                {
                    startInfo.ArgumentList.Add("--ffmpeg-location");
                    startInfo.ArgumentList.Add(ToolPaths.Ffmpeg);
                }

                if (request.EmbedThumbnail)
                {
                    startInfo.ArgumentList.Add("--embed-thumbnail");
                }

                if (request.EmbedChapters)
                {
                    startInfo.ArgumentList.Add("--embed-chapters");
                }

                if (request.EmbedSubtitles)
                {
                    startInfo.ArgumentList.Add("--embed-subs");
                    startInfo.ArgumentList.Add("--sub-langs");
                    startInfo.ArgumentList.Add("all");
                }

                if (request.UseAria2)
                {
                    // ToolPaths so a bundled aria2c.exe wins over whatever is on PATH.
                    // yt-dlp derives the --downloader-args key from the file name, so the
                    // "aria2c:" prefix below still matches even when this is a full path.
                    startInfo.ArgumentList.Add("--downloader");
                    startInfo.ArgumentList.Add(ToolPaths.Aria2);

                    var connections = Math.Clamp(request.Aria2Connections, 1, 16);
                    var splits = Math.Clamp(request.Aria2Splits, 1, 64);

                    startInfo.ArgumentList.Add("--downloader-args");
                    startInfo.ArgumentList.Add(
                        $"aria2c:-x{connections} -s{splits} -k{request.Aria2MinSplitSize}");
                }

                if (!string.IsNullOrWhiteSpace(request.TrimStart) ||
                    !string.IsNullOrWhiteSpace(request.TrimEnd))
                {
                    // yt-dlp hands this range to ffmpeg. "inf" means "to the end".
                    var from = string.IsNullOrWhiteSpace(request.TrimStart) ? "0" : request.TrimStart.Trim();
                    var to = string.IsNullOrWhiteSpace(request.TrimEnd) ? "inf" : request.TrimEnd.Trim();

                    startInfo.ArgumentList.Add("--download-sections");
                    startInfo.ArgumentList.Add($"*{from}-{to}");

                    // Without this the cut lands on the nearest keyframe and drifts.
                    startInfo.ArgumentList.Add("--force-keyframes-at-cuts");
                }

                // Without this yt-dlp redraws one progress line using \r, which never
                // arrives as a completed line and so never reaches OutputDataReceived.
                startInfo.ArgumentList.Add("--newline");

                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(request.FileNameTemplate)
                    ? DefaultTemplate
                    : request.FileNameTemplate);

                if (infoJsonPath is not null)
                {
                    startInfo.ArgumentList.Add("--load-info-json");
                    startInfo.ArgumentList.Add(infoJsonPath);
                }
                else
                {
                    startInfo.ArgumentList.Add(request.Url);
                }

                // Both of these fire on a threadpool thread, never the UI thread.
                // Reporting through IProgress is what gets the values safely to the UI.
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

                // The async wait keeps the UI responsive - WaitForExit() would freeze it.
                await process.WaitForExitAsync();

                // Returns immediately but flushes the last of the redirected output.
                process.WaitForExit();

                return process.ExitCode;
            }
            finally
            {
                if (infoJsonPath is not null && File.Exists(infoJsonPath))
                {
                    File.Delete(infoJsonPath);
                }
            }
        }
    }
}
