namespace Yoink_Downloader.Services
{
    /// <summary>
    /// Everything the Download page can ask for, in one object.
    /// DownloadAsync had grown to ten positional parameters; every new flag made the
    /// call site harder to read. Adding a property here costs nothing at the call site.
    /// </summary>
    public sealed class DownloadRequest
    {
        public string Url { get; set; } = "";

        public string OutputFolder { get; set; } = "";

        public string? FileNameTemplate { get; set; }

        // Blank means "from the beginning" / "to the end".
        public string? TrimStart { get; set; }

        public string? TrimEnd { get; set; }

        public bool EmbedThumbnail { get; set; }

        public bool EmbedSubtitles { get; set; }

        public bool EmbedChapters { get; set; }

        public bool UseAria2 { get; set; }

        /// <summary>aria2 -x. Per server, and aria2 caps it at 16.</summary>
        public int Aria2Connections { get; set; } = 16;

        /// <summary>aria2 -s. How many pieces the file is split into.</summary>
        public int Aria2Splits { get; set; } = 16;

        /// <summary>aria2 -k. Below this size aria2 will not split at all.</summary>
        public string Aria2MinSplitSize { get; set; } = "1M";
    }
}
