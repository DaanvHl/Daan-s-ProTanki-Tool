using System.Windows;
using GroupBox = System.Windows.Controls.GroupBox;
using Panel = System.Windows.Controls.Panel;

namespace Automatization.Utils;

public static class UIUtils
{
    public static GroupBox? ParentAsGroupBox(this Panel? panel)
    {
        return panel?.Parent as GroupBox;
    }

    public static void Show(this FrameworkElement element)
    {
        element.Visibility = Visibility.Visible;
    }

    public static void Hide(this FrameworkElement element)
    {
        element.Visibility = Visibility.Collapsed;
    }

    public static void ClearChildren(this Panel panel)
    {
        panel.Children.Clear();
    }
}
