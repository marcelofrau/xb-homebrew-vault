using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XBVault.Controls;

public class IconTextBlock : Panel
{
    public static readonly StyledProperty<string> IconSourceProperty =
        AvaloniaProperty.Register<IconTextBlock, string>(nameof(IconSource));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<IconTextBlock, string>(nameof(Text));

    public string IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    static IconTextBlock()
    {
        AffectsRender<IconTextBlock>(IconSourceProperty, TextProperty);
    }
}
