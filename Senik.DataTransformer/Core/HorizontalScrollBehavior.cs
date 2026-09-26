using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Senik.DataTransformer.Core
{
    public static class HorizontalScrollBehavior
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(HorizontalScrollBehavior),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseWheel += Element_PreviewMouseWheel;
                }
                else
                {
                    element.PreviewMouseWheel -= Element_PreviewMouseWheel;
                }
            }
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool isCtrlDown = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (isShiftDown || isCtrlDown)
            {
                var scrollViewer = sender as ScrollViewer ?? FindVisualChild<ScrollViewer>(sender as DependencyObject);
                if (scrollViewer != null)
                {
                    // پیمایش افقی متناسب با چرخش ماوس
                    double newOffset = scrollViewer.HorizontalOffset - e.Delta;
                    scrollViewer.ScrollToHorizontalOffset(newOffset);
                    e.Handled = true;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
