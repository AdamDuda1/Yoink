using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yoink_Downloader.Services;

namespace Yoink_Downloader.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private string _lastOutputLine = "";

        public SettingsPage()
        {
            InitializeComponent();

            YtDlpPathBox.Text = ToolPaths.YtDlpOverride ?? "";
            FfmpegPathBox.Text = ToolPaths.FfmpegOverride ?? "";
            Aria2PathBox.Text = ToolPaths.Aria2Override ?? "";

            YtDlpPathBox.TextChanged += (_, _) => ToolPaths.YtDlpOverride = YtDlpPathBox.Text.Trim();
            FfmpegPathBox.TextChanged += (_, _) => ToolPaths.FfmpegOverride = FfmpegPathBox.Text.Trim();
            Aria2PathBox.TextChanged += (_, _) => ToolPaths.Aria2Override = Aria2PathBox.Text.Trim();
        }

        private async void OnBrowseYtDlpClick(object sender, RoutedEventArgs e)
        {
            var path = await PickerHelper.PickFileAsync(".exe");
            if (path is not null)
            {
                YtDlpPathBox.Text = path;
            }
        }

        private async void OnBrowseFfmpegClick(object sender, RoutedEventArgs e)
        {
            var path = await PickerHelper.PickFileAsync(".exe");
            if (path is not null)
            {
                FfmpegPathBox.Text = path;
            }
        }

        private async void OnBrowseAria2Click(object sender, RoutedEventArgs e)
        {
            var path = await PickerHelper.PickFileAsync(".exe");
            if (path is not null)
            {
                Aria2PathBox.Text = path;
            }
        }

        private async void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            var folder = await PickerHelper.PickFolderAsync();
            if (folder is not null)
            {
                DefaultFolderBox.Text = folder;
            }
        }

        private void OnInstallYtDlpClick(object sender, RoutedEventArgs e) =>
            _ = InstallAsync(WingetService.YtDlpPackage, "yt-dlp");

        private void OnInstallFfmpegClick(object sender, RoutedEventArgs e) =>
            _ = InstallAsync(WingetService.FfmpegPackage, "ffmpeg");

        private void OnInstallAria2Click(object sender, RoutedEventArgs e) =>
            _ = InstallAsync(WingetService.Aria2Package, "aria2");

        private async System.Threading.Tasks.Task InstallAsync(string packageId, string toolName)
        {
            SetInstallButtonsEnabled(false);
            InstallProgress.Visibility = Visibility.Visible;
            ToolsInfoBar.IsOpen = false;
            InstallStatusText.Text = $"Installing {toolName}...";
            _lastOutputLine = "";

            var log = new Progress<string>(line =>
            {
                _lastOutputLine = line;
                InstallStatusText.Text = line;
                Debug.WriteLine(line);
            });

            try
            {
                var exitCode = await WingetService.InstallAsync(packageId, log);

                if (exitCode == 0)
                {
                    InstallStatusText.Text = "";
                    ShowInfo($"{toolName} installed. It is on PATH now - no path needed above.",
                        InfoBarSeverity.Success);
                }
                else
                {
                    InstallStatusText.Text = _lastOutputLine;
                    ShowInfo($"winget exited with code {exitCode}. It may need admin rights, " +
                             $"or {toolName} may already be installed.", InfoBarSeverity.Error);
                }
            }
            catch (Win32Exception)
            {
                InstallStatusText.Text = "";
                ShowInfo("Could not start winget. It ships with Windows 11 and Windows 10 1809+, " +
                         "but is missing here.", InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                InstallStatusText.Text = "";
                ShowInfo(ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                SetInstallButtonsEnabled(true);
                InstallProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void SetInstallButtonsEnabled(bool enabled)
        {
            InstallYtDlpButton.IsEnabled = enabled;
            InstallFfmpegButton.IsEnabled = enabled;
            InstallAria2Button.IsEnabled = enabled;
        }

        private void ShowInfo(string message, InfoBarSeverity severity)
        {
            ToolsInfoBar.Message = message;
            ToolsInfoBar.Severity = severity;
            ToolsInfoBar.IsOpen = true;
        }
    }
}
