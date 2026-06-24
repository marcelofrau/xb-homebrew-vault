using Avalonia;
using Avalonia.Controls;

namespace XBVault.Controls;

public static class TreeViewItemProperties
{
    public static readonly AttachedProperty<bool> IsLastChildProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItem, bool>("IsLastChild", typeof(TreeViewItemProperties));

    public static bool GetIsLastChild(TreeViewItem element) =>
        element.GetValue(IsLastChildProperty);

    public static void SetIsLastChild(TreeViewItem element, bool value) =>
        element.SetValue(IsLastChildProperty, value);
}