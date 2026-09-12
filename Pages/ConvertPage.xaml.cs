using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yoink_Downloader.Services;

namespace Yoink_Downloader.Pages
{
    /// <summary>ffmpeg on its own - convert and/or trim a file that is already on disk.</summary>
    public sealed partial class ConvertPage : Page
    {
        private string _lastOutputLine = "";

        public ConvertPage()
        {
            InitializeComponent();
        }

        private async void OnBrowseInputClick(object sender, RoutedEventArgs e)
        {
            var file = await PickerHelper.PickFileAsync(
                ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4a", ".mp3", ".wav", ".opus");

            if (file is not null)
            {
                InputBox.Text = file;

                // Default the destination to wherever the source came from.
                if (FolderBox.Text.Trim().Length == 0)
                {
                    FolderBox.Text = Path.GetDirectoryName(file) ?? "";
                }
            }
        }

        private async void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            var folder = await PickerHelper.PickFolderAsync();
            if (folder is not null)
            {
                FolderBox.Text = folder;
            }
        }

        private async void OnConvertClick(object sender, RoutedEventArgs e)
        {
            var input = InputBox.Text.Trim();
            if (input.Length == 0)
            {
                ShowInfo("Pick a file first.", InfoBarSeverity.Warning);
                return;
            }

            var folder = FolderBox.Text.Trim();
            if (folder.Length == 0)
            {
                folder = Path.GetDirectoryName(input) ?? "";
            }

            var extension = (FormatBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp4";
            var outputFile = Path.Combine(folder,
                $"{Path.GetFileNameWithoutExtension(input)}.{extension}");

            // Never let ffmpeg read and write the same path - it would truncate the source.
            if (string.Equals(outputFile, input, StringComparison.OrdinalIgnoreCase))
            {
                outputFile = Path.Combine(folder,
                    $"{Path.GetFileNameWithoutExtension(input)}-converted.{extension}");
            }

            ConvertButton.IsEnabled = false;
            ConvertProgress.Visibility = Visibility.Visible;
            ConvertInfoBar.IsOpen = false;
            StatusText.Text = "Running ffmpeg...";
            _lastOutputLine = "";

            // Constructed on the UI thread, so its callback is delivered there too - the
            // ffmpeg output itself arrives on a background thread.
            var log = new Progress<string>(line =>
            {
                _lastOutputLine = line;
                Debug.WriteLine(line);
            });

            try
            {
                var exitCode = await FfmpegService.ConvertAsync(
                    input, outputFile,
                    TrimStartBox.Text, TrimEndBox.Text,
                    CopyStreamsCheck.IsChecked == true,
                    log);

                if (exitCode == 0)
                {
                    StatusText.Text = "Done.";
                    ShowInfo($"Saved {Path.GetFileName(outputFile)}", InfoBarSeverity.Success);
                }
                else
                {
                    StatusText.Text = _lastOutputLine;
                    ShowInfo($"ffmpeg exited with code {exitCode}.", InfoBarSeverity.Error);
                }
            }
            catch (Win32Exception)
            {
                StatusText.Text = "";
                ShowInfo("Could not start ffmpeg.exe - put it on PATH or next to the app.",
                    InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "";
                ShowInfo(ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                ConvertButton.IsEnabled = true;
                ConvertProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            InputBox.Text = "";
            FolderBox.Text = "";
            TrimStartBox.Text = "";
            TrimEndBox.Text = "";
            FormatBox.SelectedIndex = 0;
            CopyStreamsCheck.IsChecked = false;
            ConvertInfoBar.IsOpen = false;
            StatusText.Text = "";
            ConvertProgress.Visibility = Visibility.Collapsed;
        }

        private void ShowInfo(string message, InfoBarSeverity severity)
        {
            ConvertInfoBar.Message = message;
            ConvertInfoBar.Severity = severity;
            ConvertInfoBar.IsOpen = true;
        }
    }
}
