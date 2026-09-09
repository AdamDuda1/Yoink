using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
//using Yoink_Downloader_Services;

namespace Yoink_Downloader.Pages
{
    public sealed partial class DownloadPage : Page
    {
        /// <summary>
        /// XAML builds the controls top-to-bottom during InitializeComponent, and setting
        /// SelectedIndex / Text in markup fires the change handlers right there - while the
        /// fields declared further down the file are still null. Guard every handler with
        /// this or you get a NullReferenceException before the page is even shown.
        /// </summary>
        private bool _isLoaded;

        public DownloadPage()
        {
            InitializeComponent();
            _isLoaded = true;
        }

        // Shared hook for every option control. The signature is deliberately loose so
        // TextChanged / SelectionChanged / Toggled / Checked can all point at it.
        private void OnOptionsChanged(object sender, object e)
        {
            if (!_isLoaded)
            {
                return;
            }

            // TODO: rebuild the yt-dlp command / enable the download button.
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            // TODO: read the clipboard into UrlBox.
        }

        private void OnFetchClick(object sender, RoutedEventArgs e)
        {
            // TODO: ask yt-dlp for the metadata and fill in the info card.
            ShowInfo("Not wired up yet.", InfoBarSeverity.Informational);
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            // TODO: folder picker -> FolderBox.Text.
        }

        private void OnAddToQueueClick(object sender, RoutedEventArgs e)
        {
            // TODO: push the current options onto the queue.
            Yoink_Downloader_Services.YtDlpService.tr();
            ShowInfo("Not wired up yet.", InfoBarSeverity.Informational);
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            UrlBox.Text = "";
            FolderBox.Text = "";
            TemplateBox.Text = "";
            SourceInfoBar.IsOpen = false;
        }

        private void ShowInfo(string message, InfoBarSeverity severity)
        {
            SourceInfoBar.Message = message;
            SourceInfoBar.Severity = severity;
            SourceInfoBar.IsOpen = true;
        }
    }
}
