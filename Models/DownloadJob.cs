namespace Yoink_Downloader.Models
{
    /// <summary>
    /// Plain display model for the rows on the Queue page. No logic yet - just enough
    /// shape for the ListView template to bind against.
    /// </summary>
    public sealed class DownloadJob
    {
        public string Title { get; set; } = "";

        public string FormatLabel { get; set; } = "";

        public string StatusText { get; set; } = "";

        /// <summary>0-100.</summary>
        public double Progress { get; set; }
    }
}
