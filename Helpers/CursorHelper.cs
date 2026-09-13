using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yoink_Downloader.Helpers;

public static class CursorHelper
{
    private static readonly PropertyInfo ProtectedCursorProperty =
        typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static readonly DependencyProperty ForceArrowCursorProperty =
        DependencyProperty.RegisterAttached(
            "ForceArrowCursor",
            typeof(bool),
            typeof(CursorHelper),
            new PropertyMetadata(false, OnForceArrowCursorChanged));

    public static void SetForceArrowCursor(UIElement element, bool value) =>
        element.SetValue(ForceArrowCursorProperty, value);

    public static bool GetForceArrowCursor(UIElement element) =>
        (bool)element.GetValue(ForceArrowCursorProperty);

    private static void OnForceArrowCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && e.NewValue is true)
        {
            element.PointerMoved += (s, args) =>
            {
                var cursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ProtectedCursorProperty.SetValue(element, cursor);
            };
            element.PointerPressed += (s, args) =>
            {
                var cursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ProtectedCursorProperty.SetValue(element, cursor);
            };
            
            if (element is ComboBox comboBox)
            {
                comboBox.DropDownOpened += (s, args) =>
                {
                    var cursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                    ProtectedCursorProperty.SetValue(comboBox, cursor);
                    comboBox.DispatcherQueue.TryEnqueue(() => HookComboBoxItems(comboBox));
                };
            }
        }
    }

    private static void HookComboBoxItems(ComboBox comboBox)
    {
        foreach (var item in comboBox.Items)
        {
            if (comboBox.ContainerFromItem(item) is ComboBoxItem container &&
                !GetForceArrowCursor(container))
            {
                SetForceArrowCursor(container, true);
            }
        }
    }
}