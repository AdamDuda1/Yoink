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

        private readonly SettingsService _settingsService = new();

        /// <summary>Guards against saving the values right back while the page is still applying them on load.</summary>
        private bool _isLoaded;

        public SettingsPage()
        {
            InitializeComponent();

            _settingsService.Load();
            var settings = _settingsService.Current;

            ToolPaths.YtDlpOverride = settings.YtDlpPath;
            ToolPaths.FfmpegOverride = settings.FfmpegPath;
            ToolPaths.Aria2Override = settings.Aria2Path;

            YtDlpPathBox.Text = settings.YtDlpPath ?? "";
            FfmpegPathBox.Text = settings.FfmpegPath ?? "";
            Aria2PathBox.Text = settings.Aria2Path ?? "";

            YtDlpPathBox.TextChanged += (_, _) => { ToolPaths.YtDlpOverride = YtDlpPathBox.Text.Trim(); SaveToolPaths(); };
            FfmpegPathBox.TextChanged += (_, _) => { ToolPaths.FfmpegOverride = FfmpegPathBox.Text.Trim(); SaveToolPaths(); };
            Aria2PathBox.TextChanged += (_, _) => { ToolPaths.Aria2Override = Aria2PathBox.Text.Trim(); SaveToolPaths(); };

            SelectComboByTag(PaneDisplayModeCombo, settings.PaneDisplayMode);
            SelectComboByTag(ThemeCombo, settings.Theme);

            _isLoaded = true;
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            foreach (var obj in combo.Items)
            {
                if (obj is ComboBoxItem { Tag: string itemTag } item && itemTag == tag)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private void SaveToolPaths()
        {
            _settingsService.Current.YtDlpPath = ToolPaths.YtDlpOverride;
            _settingsService.Current.FfmpegPath = ToolPaths.FfmpegOverride;
            _settingsService.Current.Aria2Path = ToolPaths.Aria2Override;
            _settingsService.Save();
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


        private void PaneDisplayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaneDisplayModeCombo.SelectedItem is not ComboBoxItem { Tag: string tag })
            {
                return;
            }

            App.MainWindow!.SetPaneDisplayMode(Enum.Parse<NavigationViewPaneDisplayMode>(tag));

            if (_isLoaded)
            {
                _settingsService.Current.PaneDisplayMode = tag;
                _settingsService.Save();
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string tag })
            {
                return;
            }

            if (App.MainWindow?.Content is FrameworkElement root &&
                Enum.TryParse<ElementTheme>(tag, out var theme))
            {
                root.RequestedTheme = theme;
            }

            if (_isLoaded)
            {
                _settingsService.Current.Theme = tag;
                _settingsService.Save();
            }
        }
    }
}
