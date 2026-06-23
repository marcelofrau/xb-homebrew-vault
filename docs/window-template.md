# Window Template Guide

How to create a new window matching the existing XBVault look and feel.

## Quick reference

Every window follows this pattern:

```
Window (WindowDecorations="None", Background="{StaticResource SurfaceBrush}")
  └── Border (BorderBrush="#447F3E", BorderThickness="2", Margin="1")
       └── Grid (RowDefinitions="Auto,*")
            ├── Grid (title bar, height="32", PointerPressed="OnTitleBarPointerPressed")
            │    ├── LinearGradientBrush (#447F3E → #9ACA3C) background
            │    ├── TextBlock (window title, #1A1A1A)
            │    └── Button (close, 32×32, red hover #CC3333)
            └── Border (content area, Padding="20")
                 └── your content here
```

## Full template

### `Views/MyNewWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:XBVault.ViewModels"
        x:Class="XBVault.Views.MyNewWindow"
        x:DataType="vm:MyNewViewModel"
        Title="My New Window"
        WindowStartupLocation="CenterOwner"
        Width="600" Height="400"
        CanResize="False"
        ShowInTaskbar="False"
        WindowDecorations="None"
        Background="{StaticResource SurfaceBrush}">
  <Window.Styles>
    <Style Selector="Window">
      <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
    </Style>
  </Window.Styles>
  <Border BorderBrush="#447F3E" BorderThickness="2" Margin="1">
    <Grid RowDefinitions="Auto,*">
      <!-- Title Bar -->
      <Grid Height="32" PointerPressed="OnTitleBarPointerPressed">
        <Grid.Background>
          <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%">
            <GradientStop Color="#447F3E" Offset="0"/>
            <GradientStop Color="#9ACA3C" Offset="1"/>
          </LinearGradientBrush>
        </Grid.Background>
        <Grid.ColumnDefinitions>*,Auto</Grid.ColumnDefinitions>
        <TextBlock Text="My New Window"
                   Foreground="#1A1A1A" FontSize="14" FontWeight="Bold"
                   VerticalAlignment="Center" Margin="12,0"/>
        <Button Grid.Column="1" Width="32" Height="32" Padding="0"
                BorderThickness="0" Click="OnCloseClick" Cursor="Hand"
                ToolTip.Tip="Close">
          <Image Source="avares://XBVault/Assets/Views/MyNewWindow/mynew-close-20.png"
                 Width="20" Height="20" Stretch="Uniform"/>
          <Button.Styles>
            <Style Selector="Button"><Setter Property="Background" Value="Transparent"/></Style>
            <Style Selector="Button:pointerover"><Setter Property="Background" Value="#CC3333"/></Style>
            <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
              <Setter Property="Background" Value="#CC3333"/>
            </Style>
          </Button.Styles>
        </Button>
      </Grid>
      <!-- Content -->
      <Border Grid.Row="1" Padding="20">
        <!-- Your content here -->
      </Border>
    </Grid>
  </Border>
</Window>
```

### `Views/MyNewWindow.axaml.cs`

```csharp
using Avalonia.Controls;
using Avalonia.Input;

namespace XBVault.Views;

public partial class MyNewWindow : Window
{
    public MyNewWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
```

If the window needs to trigger data load on open, add the `Opened` event:

```csharp
public MyNewWindow()
{
    InitializeComponent();
    Opened += OnOpened;
}

private void OnOpened(object? sender, EventArgs e)
{
    if (DataContext is MyNewViewModel vm)
        vm.Initialize();
}
```

### Registration in App.axaml.cs

To open the window, create it and call `ShowDialog(main)`:

```csharp
someViewModel.ShowMyNewAction = () =>
{
    var vm = new MyNewViewModel(xboxService);
    var win = new Views.MyNewWindow { DataContext = vm };
    win.ShowDialog(main);
};
```

The `ShowXxxAction` property is an `Action` on the parent ViewModel. Assign it once in
the main startup flow (`ShowMainWindow` in `App.axaml.cs`).

## Anatomy of window parts

### Window element

```xml
<Window ...
        WindowDecorations="None"   → removes OS chrome (title bar, border)
        Background="{StaticResource SurfaceBrush}"  → dark gray #1A1D23
        ShowInTaskbar="False"      → dialog windows don't appear in taskbar
        WindowStartupLocation="CenterOwner"  → centered on parent window
        CanResize="False">
```

### Outer Border

```xml
<Border BorderBrush="#447F3E" BorderThickness="2" Margin="1">
```

- `BorderBrush="#447F3E"` → dark green border
- `BorderThickness="2"` → 2px green border
- `Margin="1"` → 1px gap exposing the window's background (visual separation)

### Title bar

```xml
<Grid Height="32" PointerPressed="OnTitleBarPointerPressed">
  <Grid.Background>
    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%">
      <GradientStop Color="#447F3E" Offset="0"/>
      <GradientStop Color="#9ACA3C" Offset="1"/>
    </LinearGradientBrush>
  </Grid.Background>
  <Grid.ColumnDefinitions>*,Auto</Grid.ColumnDefinitions>
  <TextBlock Text="Window Title" Foreground="#1A1A1A"
             FontSize="14" FontWeight="Bold" VerticalAlignment="Center" Margin="12,0"/>
  <Button Grid.Column="1" Width="32" Height="32" Padding="0"
          BorderThickness="0" Click="OnCloseClick" Cursor="Hand" ToolTip.Tip="Close">
    <Image Source="..." Width="20" Height="20" Stretch="Uniform"/>
    <Button.Styles>
      <Style Selector="Button"><Setter Property="Background" Value="Transparent"/></Style>
      <Style Selector="Button:pointerover"><Setter Property="Background" Value="#CC3333"/></Style>
    </Button.Styles>
  </Button>
</Grid>
```

- Gradient: `#447F3E` (dark green) → `#9ACA3C` (bright green)
- Close button is 32×32, transparent by default, red (`#CC3333`) on hover
- Close button icon: `{viewname}-close-20.png`
- `PointerPressed="OnTitleBarPointerPressed"` enables window dragging
- Title text is `#1A1A1A` (dark) — it's on the green gradient

### Content area

```xml
<Border Grid.Row="1" Padding="20">
```

Standard 20px padding inside the green border. Place your controls here.

## Theming reference

All colors and brushes are defined in `Assets/Themes/BladesTheme.axaml`.

| Resource | Color | Usage |
|----------|-------|-------|
| `BgBrush` | `#0D1117` | Main window background |
| `SurfaceBrush` | `#1A1D23` | Dialog window background |
| `SurfaceAltBrush` | `#252830` | Card/list backgrounds |
| `BorderBrush` | `#2A2D33` | Generic borders |
| `AccentBrush` | `#9ACA3C` | Green accent |
| `AccentDimBrush` | `#6B8F2A` | Dimmed green |
| `TextBrush` | `#F0F0F0` | Primary text |
| `TextMutedBrush` | `#8B8D91` | Secondary text |
| `TextDimBrush` | `#5A5C60` | Dim text |
| `DangerBrush` | `#E74C3C` | Error/danger |
| `CardBgBrush` | `#1E2128` | Card background |

## Icons for new windows

1. Open `F:\workspace\icons8-personal-set\{size}x{size}/`
2. Copy the PNG matching your need
3. Rename to `{viewname}-{descriptor}-{size}.png` (see `docs/ASSETS-GUIDE.md`)
4. Place in `XBVault/Assets/Views/{ViewName}/`
5. Reference in axaml as `avares://XBVault/Assets/Views/{ViewName}/{filename}`

Minimum required icons:
- `{viewname}-close-20.png` — close button (20×20, reusable from `connection-close-20.png` if appropriate)

## Checklist for a new window

- [ ] Create `Views/MyNewWindow.axaml` (template above)
- [ ] Create `Views/MyNewWindow.axaml.cs` (code-behind with drag + close)
- [ ] Create `ViewModels/MyNewViewModel.cs` (receives services, exposes bindings)
- [ ] Create `Assets/Views/MyNewWindow/` and add at least the close icon
- [ ] Register in `App.axaml.cs` via `ShowXxxAction = () => ...`
- [ ] Build and verify: `dotnet build XBVault/XBVault.csproj`
