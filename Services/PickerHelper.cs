using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Yoink_Downloader_Services
{
    /// <summary>
    /// WinUI 3 pickers are not window-aware on their own - each one has to be handed the
    /// shell window's HWND before it is shown, or it throws. This wraps that boilerplate.
    /// </summary>
    public static class PickerHelper
    {
        public static async Task<string?> PickFolderAsync()
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            Initialize(picker);

            StorageFolder? folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        public static async Task<string?> PickFileAsync(params string[] extensions)
        {
            var picker = new FileOpenPicker();

            if (extensions.Length == 0)
            {
                picker.FileTypeFilter.Add("*");
            }
            else
            {
                foreach (var extension in extensions)
                {
                    picker.FileTypeFilter.Add(extension);
                }
            }

            Initialize(picker);

            StorageFile? file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private static void Initialize(object picker)
        {
            if (Yoink_Downloader.App.MainWindow is null)
            {
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Yoink_Downloader.App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
