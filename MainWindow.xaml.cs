using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Yoink_Downloader.Pages;

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
    }

    public class CursorGrid : Grid
    {
        public InputSystemCursorShape CursorShape
        {
            set => ProtectedCursor = InputSystemCursor.Create(value);
        }
    }
}
