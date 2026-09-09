using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yoink_Downloader.Models;

namespace Yoink_Downloader.Pages
{
    public sealed partial class QueuePage : Page
    {
        public QueuePage()
        {
            InitializeComponent();

            // Placeholder rows so the layout is visible. Swap for the real collection later.
            JobList.ItemsSource = new List<DownloadJob>
            {
                new() { Title = "Sample video", FormatLabel = "mp4 1080p", StatusText = "Queued", Progress = 0 },
                new() { Title = "Another sample", FormatLabel = "mp3", StatusText = "Downloading - 42%", Progress = 42 }
            };
        }

        private void OnStartAllClick(object sender, RoutedEventArgs e)
        {
            // TODO: kick off the queue.
        }

        private void OnClearFinishedClick(object sender, RoutedEventArgs e)
        {
            // TODO: drop completed / failed rows.
        }
    }
}
