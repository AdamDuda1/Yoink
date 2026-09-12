using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Yoink_Downloader.Services
{
    /// <summary>
    /// One entry from yt-dlp's "formats" array.
    /// Everything nullable on purpose as different
    /// entry types have different properties
    /// </summary>
    public record VideoFormat(
        string FormatId,
        string Extension,
        int? Height,
        int? Width,
        double? Fps,
        string? VideoCodec,
        string? AudioCodec,
        long? FileSize,
        double? Bitrate,
        string? Note)
    {
        public bool IsAudioOnly =>
            string.IsNullOrEmpty(VideoCodec) || VideoCodec == "none";

        public bool IsVideoOnly =>
            string.IsNullOrEmpty(AudioCodec) || AudioCodec == "none";

        public bool IsComplete => !IsAudioOnly && !IsVideoOnly;

        /// <summary>"1080p60 mp4 (avc1)" or "audio only m4a" - for a dropdown.</summary>
        public string Label
        {
            get
            {
                if (IsAudioOnly)
                {
                    var rate = Bitrate is > 0 ? $" {Bitrate:0}k" : "";
                    return $"audio only  {Extension}{rate}";
                }

                var resolution = Height is > 0 ? $"{Height}p" : (Note ?? "video");
                var fps = Fps is > 30 ? $"{Fps:0}" : "";
                var kind = IsVideoOnly ? "  (video only)" : "";
                return $"{resolution}{fps}  {Extension}{kind}";
            }
        }

        public string SizeLabel => FileSize is > 0
            ? $"{FileSize.Value / 1024.0 / 1024.0:0.#} MB"
            : "";
    }

    /// <summary>
    /// One entry from yt-dlp's "thumbnails" array.
    ///
    /// Only url, id and preference are reliably present - in the sample dump just 7 of
    /// 46 entries carried width/height. So order by Preference, not by size.
    /// </summary>
    public record VideoThumbnail(string Id, int Preference, int? Width, int? Height, string Url)
    {
        public bool HasDimensions => Width is > 0 && Height is > 0;

        /// <summary>"1920x1080 (jpg)" when known, otherwise falls back to the id.</summary>
        public string Label
        {
            get
            {
                var extension = GuessExtension();
                return HasDimensions ? $"{Width}x{Height}  {extension}" : $"#{Id}  {extension}";
            }
        }

        /// <summary>The URL has no clean extension field, so read it off the path.</summary>
        public string GuessExtension()
        {
            var path = Url.Split('?')[0];
            var dot = path.LastIndexOf('.');
            return dot >= 0 && path.Length - dot <= 6 ? path[(dot + 1)..] : "img";
        }
    }

    /// <summary>Parsers for the two arrays. Kept apart from YtDlpService so it stays readable.</summary>
    public static class MediaFormats
    {
        public static IReadOnlyList<VideoFormat> ParseFormats(JsonElement root)
        {
            var results = new List<VideoFormat>();

            if (!root.TryGetProperty("formats", out var formats) ||
                formats.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var entry in formats.EnumerateArray())
            {
                var id = Str(entry, "format_id");
                if (id is null)
                {
                    continue;
                }

                results.Add(new VideoFormat(
                    FormatId: id,
                    Extension: Str(entry, "ext") ?? "",
                    Height: Int(entry, "height"),
                    Width: Int(entry, "width"),
                    Fps: Num(entry, "fps"),
                    VideoCodec: Str(entry, "vcodec"),
                    AudioCodec: Str(entry, "acodec"),
                    FileSize: Long(entry, "filesize") ?? Long(entry, "filesize_approx"),
                    Bitrate: Num(entry, "tbr"),
                    Note: Str(entry, "format_note")));
            }

            return results;
        }

        public static IReadOnlyList<VideoThumbnail> ParseThumbnails(JsonElement root)
        {
            var results = new List<VideoThumbnail>();

            if (!root.TryGetProperty("thumbnails", out var thumbnails) ||
                thumbnails.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var entry in thumbnails.EnumerateArray())
            {
                var url = Str(entry, "url");
                if (url is null)
                {
                    continue;
                }

                results.Add(new VideoThumbnail(
                    Id: Str(entry, "id") ?? "",
                    Preference: Int(entry, "preference") ?? int.MinValue,
                    Width: Int(entry, "width"),
                    Height: Int(entry, "height"),
                    Url: url));
            }

            return results.OrderByDescending(t => t.Preference).ToList();
        }

        /// <summary>
        /// Distinct heights, biggest first - the list to put behind a "quality" dropdown.
        /// Feed a value into "-f bv*[height&lt;=N]+ba/b".
        /// </summary>
        public static IReadOnlyList<int> AvailableHeights(IEnumerable<VideoFormat> formats) =>
            formats.Where(f => f.Height is > 0)
                   .Select(f => f.Height!.Value)
                   .Distinct()
                   .OrderByDescending(h => h)
                   .ToList();

        private static string? Str(JsonElement element, string name) =>
            element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static int? Int(JsonElement element, string name) =>
            element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetDouble(out var d)
                ? (int)d
                : null;

        private static long? Long(JsonElement element, string name) =>
            element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetDouble(out var d)
                ? (long)d
                : null;

        private static double? Num(JsonElement element, string name) =>
            element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetDouble(out var d)
                ? d
                : null;
    }
}
