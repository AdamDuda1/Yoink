using System;
using Windows.Graphics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Yoink_Downloader.Pages;
using Yoink_Downloader.Services;

namespace Yoink_Downloader
{
    /// <summary>Shell window: title bar + left nav + a Frame the pages get loaded into.</summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ContentFrame.Navigate(typeof(DownloadPage));

            var settings = new SettingsService();
            settings.Load();
            if (Content is FrameworkElement root && Enum.TryParse<ElementTheme>(settings.Current.Theme, out var theme))
            {
                root.RequestedTheme = theme;
            }

            var nonClientSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
            nonClientSource.SetRegionRects(NonClientRegionKind.Passthrough, Array.Empty<RectInt32>());
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item)
            {
                return;
            }

            Type target = item.Tag switch
            {
                "convert" => typeof(ConvertPage),
                "queue" => typeof(QueuePage),
                "settings" => typeof(SettingsPage),
                _ => typeof(DownloadPage)
            };

            if (ContentFrame.CurrentSourcePageType != target)
            {
                ContentFrame.Navigate(target, null, new EntranceNavigationTransitionInfo());
            }
        }

        private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        public void SetPaneDisplayMode(NavigationViewPaneDisplayMode mode)
        {
            NavView.PaneDisplayMode = mode;
            AppTitleBar.IsPaneToggleButtonVisible = mode != NavigationViewPaneDisplayMode.Top;
        }
    }

    public class CursorGrid : Grid
    {
        public InputSystemCursorShape CursorShape
        {
            set => ProtectedCursor = InputSystemCursor.Create(value);
        }
    }
}
