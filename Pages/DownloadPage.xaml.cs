using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Yoink_Downloader.Services;

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

        private SettingsService _settingsService = new();

        public DownloadPage()
        {
            InitializeComponent();
            _isLoaded = true;

            _settingsService.Load();
            var options = _settingsService.Current.VideoDownloadOptions;
            EmbedThumbnailCheckbox.IsChecked = options.EmbedThumbnail;
            EmbedSubtitlesCheckbox.IsChecked = options.EmbedSubtitles;
            EmbedChaptersCheckbox.IsChecked = options.EmbedChapters;

            FolderBox.Text = DefaultDownloadFolder();
        }

        private static string DefaultDownloadFolder() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // Shared hook for every option control. The signature is deliberately loose so
        // TextChanged / SelectionChanged / Toggled / Checked can all point at it.
        private void OnDownloadOptionsChanged(object sender, object e)
        {
            if (!_isLoaded)
            {
                return;
            }

            _settingsService.Current.VideoDownloadOptions.EmbedThumbnail = EmbedThumbnailCheckbox.IsChecked.Equals(true);
            _settingsService.Current.VideoDownloadOptions.EmbedSubtitles = EmbedSubtitlesCheckbox.IsChecked.Equals(true);
            _settingsService.Current.VideoDownloadOptions.EmbedChapters = EmbedChaptersCheckbox.IsChecked.Equals(true);
            _settingsService.Save();

            Debug.WriteLine("Shots fired.");
        }

        private async void OnPasteClick(object sender, RoutedEventArgs e)
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                UrlBox.Text = (await content.GetTextAsync()).Trim();
            }
        }

        private async void OnFetchClick(object sender, RoutedEventArgs e)
        {
            var url = UrlBox.Text.Trim();
            if (url.Length < 4)
            {
                ShowInfo("Paste a valid link first.", InfoBarSeverity.Warning);
                return;
            }
        
            SourceInfoBar.IsOpen = false;
            FetchingProgress.Visibility = Visibility.Visible;
            MetadataBlock.Visibility = Visibility.Collapsed;
            VideoTitleText.Text = "Loading...";
            VideoMetaText.Text = "";
        
            try
            {
                var info = await YtDlpService.FetchInfoAsync(url);
        
                VideoTitleText.Text = info.Title;
                VideoAuthorText.Text = info.Duration > TimeSpan.Zero
                    ? $"{info.Uploader}  ·  {info.Duration:hh\\:mm\\:ss}"
                    : info.Uploader;
                VideoMetaText.Text = info.Id + " (Payload " + info.PayloadSize + ")";
                VideoMetaTopLink.NavigateUri = new Uri(info.UploaderLink);

                if (info.ThumbnailUrl is not null)
                {
                    // BitmapImage fetches an http(s) source itself, off the UI thread.
                    ThumbnailImage.Source = new BitmapImage(new Uri(info.ThumbnailUrl));
                    ThumbnailImage.Visibility = Visibility.Visible;
                    ThumbnailPlaceholder.Visibility = Visibility.Collapsed;
                }

                FetchingProgress.Visibility = Visibility.Collapsed;
                MetadataBlock.Visibility = Visibility.Visible;
            }
            catch (Win32Exception)
            {
                VideoTitleText.Text = "No video loaded";
                ShowInfo("Could not start yt-dlp.exe - put it on PATH or next to the app.",
                    InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                VideoTitleText.Text = "No video loaded";
                ShowInfo(ex.Message, InfoBarSeverity.Error);
            }
        }
        
        // BitmapImage failures are silent otherwise - you would just get an empty box.
        private void OnThumbnailFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ThumbnailImage.Visibility = Visibility.Collapsed;
            ThumbnailPlaceholder.Visibility = Visibility.Visible;
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {

            // A ContentDialog needs a XamlRoot to know which window to show over, and
            // every link in that chain is nullable - hence the guard rather than dots.
            var xamlRoot = App.MainWindow?.Content?.XamlRoot;
            if (xamlRoot is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Read dis:",
                Content = "File Explorer is boutta open.",
                PrimaryButtonText = "Ok, go on",
                CloseButtonText = "NO NO NO NO NO NO NO CANCEL",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var folder = await PickerHelper.PickFolderAsync();
                if (folder is not null)
                {
                    FolderBox.Text = folder;
                }
            }
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
                var request = new DownloadRequest
                {
                    Url = url,
                    OutputFolder = folder,
                    FileNameTemplate = TemplateBox.Text.Trim(),
                    TrimStart = TrimStartBox.Text,
                    TrimEnd = TrimEndBox.Text,

                    EmbedThumbnail = EmbedThumbnailCheckbox.IsChecked == true,
                    EmbedSubtitles = EmbedSubtitlesCheckbox.IsChecked == true,
                    EmbedChapters = EmbedChaptersCheckbox.IsChecked == true,

                    UseAria2 = UseAria2Checkbox.IsChecked == true,
                    Aria2Connections = ComboInt(MaxConnectionsCombo, 16),
                    Aria2Splits = ComboInt(SplitsCombo, 16),
                    Aria2MinSplitSize = ComboText(MinSplitSizeCombo, "1M"),
                };

                var exitCode = await YtDlpService.DownloadAsync(request, progress, log);

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
            TrimStartBox.Text = "";
            TrimEndBox.Text = "";
            SourceInfoBar.IsOpen = false;
            StatusText.Text = "";
            DownloadProgress.Value = 0;
            DownloadProgress.Visibility = Visibility.Collapsed;

            VideoTitleText.Text = "No video loaded";
            VideoMetaText.Text = "Paste a link and hit Fetch info";
            ThumbnailImage.Source = null;
            ThumbnailImage.Visibility = Visibility.Collapsed;
            ThumbnailPlaceholder.Visibility = Visibility.Visible;
        }

        private void ShowInfo(string message, InfoBarSeverity severity)
        {
            SourceInfoBar.Message = message;
            SourceInfoBar.Severity = severity;
            SourceInfoBar.IsOpen = true;
        }



        /// <summary>Reads the selected ComboBoxItem's text, falling back if nothing is selected.</summary>
        private static string ComboText(ComboBox box, string fallback) =>
            (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

        private static int ComboInt(ComboBox box, int fallback) =>
            int.TryParse(ComboText(box, ""), out var value) ? value : fallback;

        private Visibility GetVisibility(bool? isChecked) =>
            isChecked == true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Only the "Cookies from browser" option (index 0) needs a browser picker.</summary>
        private Visibility GetVisibility(int selectedIndex) =>
            selectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

        private void Aria2ToolTip_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Aria2ToolTip.IsOpen = true;
        }
        private void Aria2ToolTip_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Aria2ToolTip.IsOpen = false;
        }

        private void PassVisitorDataToolTip_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            PassVisitorDataToolTip.IsOpen = true;
        }
        private void PassVisitorDataToolTip_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            PassVisitorDataToolTip.IsOpen = false;
        }

        private void InfoIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }
    }
}
