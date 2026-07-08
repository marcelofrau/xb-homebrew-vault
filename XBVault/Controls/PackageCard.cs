using Avalonia;
using Avalonia.Controls;

namespace XBVault.Controls;

public class PackageCard : Border
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PackageCard, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static PackageCard()
    {
        IsSelectedProperty.Changed.AddClassHandler<PackageCard>((ctrl, _) =>
            ctrl.PseudoClasses.Set(":selected", ctrl.IsSelected));
    }
}
