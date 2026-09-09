using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yoink_Downloader.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void OnBrowseYtDlpClick(object sender, RoutedEventArgs e)
        {
            // TODO: file picker -> YtDlpPathBox.Text.
        }

        private void OnBrowseFfmpegClick(object sender, RoutedEventArgs e)
        {
            // TODO: file picker -> FfmpegPathBox.Text.
        }

        private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            // TODO: folder picker -> DefaultFolderBox.Text.
        }
    }
}
