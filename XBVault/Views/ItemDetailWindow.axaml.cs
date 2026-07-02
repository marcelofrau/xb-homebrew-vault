using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class ItemDetailWindow : Window
{
    private DispatcherTimer? _spinTimer;
    private double _spinAngle;

    public ItemDetailWindow()
    {
        try
        {
            InitializeComponent();
            Logger.Info("ItemDetailWindow InitializeComponent OK");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ItemDetailWindow InitializeComponent FAILED");
            throw;
        }
        Loaded += (_, _) => {
            Logger.Info("ItemDetailWindow Loaded");
            StartSpin();
        };
        Unloaded += (_, _) => StopSpin();
    }

    private void StartSpin()
    {
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += SpinTick;
        _spinTimer.Start();
    }

    private void StopSpin()
    {
        if (_spinTimer is null) return;
        _spinTimer.Tick -= SpinTick;
        _spinTimer.Stop();
        _spinTimer = null;
    }

    private void SpinTick(object? sender, EventArgs e)
    {
        _spinAngle = (_spinAngle - 6 + 360) % 360;
        if (InstallSpinner?.RenderTransform is RotateTransform rt)
            rt.Angle = _spinAngle;
        if (CheckSpinner?.RenderTransform is RotateTransform ct)
            ct.Angle = _spinAngle;
    }

    private void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (DataContext is not BrowseViewModel vm) return;
        if (vm.SelectedItem is null) return;

        var installable = vm.SelectedItem.Downloads
            .Where(d => d.DownloadType == DownloadType.MainPackage || d.DownloadType == DownloadType.Unknown)
            .ToList();

        if (installable.Count > 1)
        {
            var flyout = new MenuFlyout();
            foreach (var download in installable)
            {
                var item = new MenuItem
                {
                    Header = new TextBlock { Text = download.Label ?? download.Url, Foreground = Brushes.White }
                };
                var captured = download;
                item.Click += async (_, _) => await vm.InstallByAssetAsync(captured);
                flyout.Items.Add(item);
            }
            flyout.ShowAt(btn);
        }
        else
        {
            vm.InstallSelectedCommand.Execute(null);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnDeveloperClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Developer" && c.Name == vm.SelectedItem.Developer);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private void OnUwpPortByClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Porter" && c.Name == vm.SelectedItem.UwpPortBy);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private void OnMaintainedByClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var vm = DataContext as BrowseViewModel;
        var contributor = vm?.SelectedItem?.Contributors
            .FirstOrDefault(c => c.Role == "Maintainer" && c.Name == vm.SelectedItem.MaintainedBy);
        if (contributor is not null)
            ShowContributorFlyout(btn, contributor);
    }

    private void OnContributorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not Contributor contributor) return;
        ShowContributorFlyout(btn, contributor);
    }

    private static void ShowContributorFlyout(Button anchor, Contributor contributor)
    {
        var flyout = new MenuFlyout();
        var hasLinks = false;

        if (!string.IsNullOrEmpty(contributor.Url))
        {
            var url = contributor.Url;
            var item = new MenuItem
            {
                Header = new TextBlock { Text = "GitHub", Foreground = Brushes.White },
                Icon = new Image
                {
                    Source = new Avalonia.Media.Imaging.Bitmap(
                        Avalonia.Platform.AssetLoader.Open(new Uri("avares://XBVault/Assets/Views/ItemDetailWindow/itemdetail-github-20.png"))),
                    Width = 16, Height = 16
                }
            };
            item.Click += (_, _) => OpenUrl(url);
            flyout.Items.Add(item);
            hasLinks = true;
        }

        if (contributor.Donations?.Count > 0)
        {
            foreach (var donation in contributor.Donations)
            {
                var label = donation.Type.ToLowerInvariant() switch
                {
                    "kofi" or "ko-fi" => "Ko-fi",
                    "patreon" => "Patreon",
                    "paypal" => "PayPal",
                    "github_sponsors" => "GitHub Sponsors",
                    "buymeacoffee" => "Buy Me a Coffee",
                    _ => donation.Type
                };
                var donUrl = donation.Url;
                var donItem = new MenuItem
                {
                    Header = new TextBlock { Text = label, Foreground = Brushes.White }
                };
                donItem.Click += (_, _) => OpenUrl(donUrl);
                flyout.Items.Add(donItem);
                hasLinks = true;
            }
        }

        if (!hasLinks)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = new TextBlock { Text = "No links available", Foreground = Brushes.Gray },
                IsEnabled = false
            });
        }

        flyout.ShowAt(anchor);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to open URL: {url}");
        }
    }
}
