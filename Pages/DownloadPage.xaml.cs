using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Yoink_Downloader_Services;

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

        /// <summary>Last line yt-dlp printed, so a failure can say something useful.</summary>
        private string _lastOutputLine = "";

        public DownloadPage()
        {
            InitializeComponent();
            _isLoaded = true;

            FolderBox.Text = DefaultDownloadFolder();
        }

        private static string DefaultDownloadFolder() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // Shared hook for every option control. The signature is deliberately loose so
        // TextChanged / SelectionChanged / Toggled / Checked can all point at it.
        private void OnOptionsChanged(object sender, object e)
        {
            if (!_isLoaded)
            {
                return;
            }
        }

        private async void OnPasteClick(object sender, RoutedEventArgs e)
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                UrlBox.Text = (await content.GetTextAsync()).Trim();
            }
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

        // async void is normally a bug, but it is exactly right for an event handler -
        // there is no caller to hand a Task back to.
        private async void OnDownloadClick(object sender, RoutedEventArgs e)
        {
            var url = UrlBox.Text.Trim();
            if (url.Length == 0)
            {
                ShowInfo("Paste a link first.", InfoBarSeverity.Warning);
                return;
            }

            var folder = FolderBox.Text.Trim();
            if (folder.Length == 0)
            {
                folder = DefaultDownloadFolder();
            }

            DownloadButton.IsEnabled = false;
            DownloadProgress.Visibility = Visibility.Visible;
            DownloadProgress.Value = 0;
            SourceInfoBar.IsOpen = false;
            StatusText.Text = "Starting yt-dlp...";
            _lastOutputLine = "";

            // Progress<T> grabs the current SynchronizationContext when it is constructed.
            // Because that happens here, on the UI thread, these callbacks are delivered
            // back on the UI thread even though yt-dlp's output arrives on a background
            // one. Touching a control from the background thread directly would throw.
            var progress = new Progress<double>(percent =>
            {
                DownloadProgress.Value = percent;
                StatusText.Text = $"Downloading... {percent:0.0}%";
            });

            var log = new Progress<string>(line =>
            {
                _lastOutputLine = line;
                Debug.WriteLine(line);
            });

            try
            {
                var exitCode = await YtDlpService.DownloadAsync(
                    url, folder, TemplateBox.Text.Trim(), progress, log);

                if (exitCode == 0)
                {
                    DownloadProgress.Value = 100;
                    StatusText.Text = "Done.";
                    ShowInfo($"Saved to {folder}", InfoBarSeverity.Success);
                }
                else
                {
                    StatusText.Text = _lastOutputLine;
                    ShowInfo($"yt-dlp exited with code {exitCode}.", InfoBarSeverity.Error);
                }
            }
            catch (Win32Exception)
            {
                // Thrown when the exe simply is not there.
                StatusText.Text = "";
                ShowInfo("Could not start yt-dlp.exe - put it on PATH or next to the app.",
                    InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "";
                ShowInfo(ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                DownloadButton.IsEnabled = true;
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            UrlBox.Text = "";
            FolderBox.Text = DefaultDownloadFolder();
            TemplateBox.Text = "";
            SourceInfoBar.IsOpen = false;
            StatusText.Text = "";
            DownloadProgress.Value = 0;
            DownloadProgress.Visibility = Visibility.Collapsed;
        }

        private void ShowInfo(string message, InfoBarSeverity severity)
        {
            SourceInfoBar.Message = message;
            SourceInfoBar.Severity = severity;
            SourceInfoBar.IsOpen = true;
        }
    }
}
